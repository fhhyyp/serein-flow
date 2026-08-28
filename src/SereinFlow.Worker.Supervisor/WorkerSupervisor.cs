using System.Diagnostics;
using System.Text;
using SereinFlow.Contracts;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Supervisor;

public sealed record RunnerLaunchOptions(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    TimeSpan? HandshakeTimeout = null,
    TimeSpan? HeartbeatInterval = null,
    TimeSpan? CancellationGracePeriod = null,
    string? AllowedScriptArtifactRoot = null,
    string? AllowedLibraryPackageRoot = null,
    Action<string>? DiagnosticLogger = null)
{
    public TimeSpan EffectiveHandshakeTimeout => HandshakeTimeout ?? TimeSpan.FromSeconds(5);

    public TimeSpan EffectiveHeartbeatInterval => HeartbeatInterval ?? TimeSpan.FromSeconds(2);

    public TimeSpan EffectiveCancellationGracePeriod => CancellationGracePeriod ?? TimeSpan.FromSeconds(3);
}

public sealed class WorkerSupervisor
{
    private readonly RunnerLaunchOptions _launchOptions;

    public WorkerSupervisor(RunnerLaunchOptions launchOptions)
    {
        if (launchOptions is null)
            throw new ArgumentNullException(nameof(launchOptions), "Worker runner launch options cannot be null. Worker Runner 启动选项不能为空。");
        if (string.IsNullOrWhiteSpace(launchOptions.FileName))
            throw new ArgumentException("Worker runner file name cannot be empty. Worker Runner 文件名不能为空。", nameof(launchOptions));
        _launchOptions = launchOptions;
    }

    public async Task<WorkerRunResultDto> RunAsync(
        WorkerRunRequestDto request,
        Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request), "The worker request cannot be null. Worker 请求不能为空。");
        if (publishEvent is null)
            throw new ArgumentNullException(nameof(publishEvent), "The worker event publisher cannot be null. Worker 事件发布器不能为空。");
        if (request.ProtocolVersion != WorkerProtocolConstants.Version)
            return Failure(request.RunId, "worker.protocol_mismatch", "The requested worker protocol version is not supported. 请求的 Worker 协议版本不受支持。");
        if (!IsAllowedPath(request.ScriptArtifactRootPath, _launchOptions.AllowedScriptArtifactRoot)
            || !IsAllowedPath(request.LibraryPackageRootPath, _launchOptions.AllowedLibraryPackageRoot))
        {
                return Failure(request.RunId, "worker.path_outside_root", "Worker artifact paths are outside the configured service roots. Worker 缓存路径超出了服务端允许的根目录。");
        }
        var runnerPath = ResolveRunnerPath();
        if (runnerPath is not null && !File.Exists(runnerPath))
        {
            var message = $"Worker Runner executable was not found at '{runnerPath}'. Worker Runner 可执行文件不存在：'{runnerPath}'。";
            RecordDiagnostic(request.RunId, "runner.path", message);
            return Failure(request.RunId, "worker.runner_not_found", message);
        }
        if (request.Deadline <= DateTimeOffset.UtcNow)
            return new WorkerRunResultDto(WorkerProtocolConstants.Version, request.RunId, FlowRunStatusDto.TimedOut, "worker.timed_out", "The run deadline elapsed before the runner started. Worker Runner 启动前运行截止时间已到。");

        using var process = StartProcess();
        using var stdout = process.StandardOutput;
        await using var writer = new WorkerMessageWriter(process.StandardInput.BaseStream);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var deadlineCancellation = new CancellationTokenSource(request.Deadline - DateTimeOffset.UtcNow);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellation.Token);

        try
        {
            var ready = await WorkerProtocolCodec.ReadAsync(
                stdout,
                line => RecordDiagnostic(request.RunId, "runner.stdout_noise", line),
                runCancellation.Token).AsTask()
                .WaitAsync(_launchOptions.EffectiveHandshakeTimeout, runCancellation.Token);
            if (ready is null)
                return await TerminateAndReturnAsync(process, request.RunId, "worker.crashed", "Runner closed its protocol stream before announcing readiness. Worker Runner 在宣布就绪前关闭了协议流。", FlowRunStatusDto.Failed);
            if (ready.Kind != WorkerProtocolConstants.ReadyKind)
                return await TerminateAndReturnAsync(process, request.RunId, "worker.handshake_failed", "Runner did not announce readiness. Worker Runner 未宣布就绪。", FlowRunStatusDto.Failed);

            await writer.WriteAsync(WorkerMessage.Create(WorkerProtocolConstants.HandshakeKind), runCancellation.Token);
            var accepted = await WorkerProtocolCodec.ReadAsync(
                stdout,
                line => RecordDiagnostic(request.RunId, "runner.stdout_noise", line),
                runCancellation.Token).AsTask()
                .WaitAsync(_launchOptions.EffectiveHandshakeTimeout, runCancellation.Token);
            if (accepted is null)
                return await TerminateAndReturnAsync(process, request.RunId, "worker.crashed", "Runner closed its protocol stream during the handshake. Worker Runner 在握手期间关闭了协议流。", FlowRunStatusDto.Failed);
            if (accepted.Kind != WorkerProtocolConstants.HandshakeAcceptedKind)
                return await TerminateAndReturnAsync(process, request.RunId, "worker.protocol_mismatch", "Runner rejected the worker protocol handshake. Worker Runner 拒绝了 Worker 协议握手。", FlowRunStatusDto.Failed);

            await writer.WriteAsync(
                WorkerMessage.Create(WorkerProtocolConstants.RunKind, WorkerProtocolCodec.SerializePayload(request), request.RunId, request.Deadline),
                runCancellation.Token);

            return await MonitorRunAsync(process, stdout, writer, request, publishEvent, deadlineCancellation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var timedOut = deadlineCancellation.IsCancellationRequested;
            return await CancelAndReturnAsync(
                process,
                stdout,
                writer,
                request,
                publishEvent,
                timedOut ? FlowRunStatusDto.TimedOut : FlowRunStatusDto.Cancelled,
                timedOut ? "worker.timed_out" : "worker.cancelled");
        }
        catch (WorkerProtocolException exception)
        {
            RecordProtocolDiagnostic(request.RunId, "protocol", exception);
            return await TerminateAndReturnAsync(process, request.RunId, exception.Code, exception.Message, FlowRunStatusDto.Failed);
        }
        catch (Exception exception)
        {
            RecordDiagnostic(request.RunId, "supervisor", exception.ToString(), exception);
            return await TerminateAndReturnAsync(process, request.RunId, "worker.crashed", "The runner exited before returning a valid result. Worker Runner 在返回有效结果前退出。", FlowRunStatusDto.Failed);
        }
        finally
        {
            if (!process.HasExited)
                TerminateProcessTree(process);
            var stderr = await ReadDiagnosticsAsync(stderrTask);
            if (!string.IsNullOrWhiteSpace(stderr))
                RecordDiagnostic(request.RunId, "runner.stderr", stderr);
        }
    }

    /// <summary>
    /// Starts a Worker process whose stdin remains owned by the supervisor for
    /// the lifetime of the returned debug session. This is deliberately a
    /// distinct API from RunAsync so a caller cannot write directly to Worker
    /// stdin or compete with its sole stdout reader.
    /// 启动调试 Worker，并在返回会话的生命周期内由 Supervisor 独占其 stdin。它与
    /// RunAsync 分开，避免调用方直接写 Worker stdin 或与唯一 stdout 读取器竞争。
    /// </summary>
    public async Task<DebugRunSession> StartDebugAsync(
        WorkerRunRequestDto request,
        Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request), "The worker request cannot be null. Worker 请求不能为空。");
        if (publishEvent is null)
            throw new ArgumentNullException(nameof(publishEvent), "The worker event publisher cannot be null. Worker 事件发布器不能为空。");
        if (request.Debug is null || request.Debug.DebugSessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A debug request requires a non-empty debug session ID. 调试请求需要非空的调试会话 ID。",
                nameof(request));
        }
        if (request.ProtocolVersion != WorkerProtocolConstants.Version)
            throw new WorkerProtocolException("worker.protocol_mismatch", "The requested worker protocol version is not supported. 请求的 Worker 协议版本不受支持。");
        if (!IsAllowedPath(request.ScriptArtifactRootPath, _launchOptions.AllowedScriptArtifactRoot)
            || !IsAllowedPath(request.LibraryPackageRootPath, _launchOptions.AllowedLibraryPackageRoot))
        {
            throw new WorkerProtocolException("worker.path_outside_root", "Worker artifact paths are outside the configured service roots. Worker 缓存路径超出了服务端允许的根目录。");
        }
        var runnerPath = ResolveRunnerPath();
        if (runnerPath is not null && !File.Exists(runnerPath))
        {
            throw new FileNotFoundException(
                $"Worker Runner executable was not found at '{runnerPath}'. Worker Runner 可执行文件不存在：'{runnerPath}'。",
                runnerPath);
        }
        if (request.Deadline <= DateTimeOffset.UtcNow)
        {
            throw new TimeoutException(
                "The run deadline elapsed before the runner started. Worker Runner 启动前运行截止时间已到。");
        }

        var process = StartProcess();
        var stdout = process.StandardOutput;
        var writer = new WorkerMessageWriter(process.StandardInput.BaseStream);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var deadlineCancellation = new CancellationTokenSource(request.Deadline - DateTimeOffset.UtcNow);
        var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellation.Token);
        try
        {
            var ready = await WorkerProtocolCodec.ReadAsync(
                stdout,
                line => RecordDiagnostic(request.RunId, "runner.stdout_noise", line),
                runCancellation.Token).AsTask()
                .WaitAsync(_launchOptions.EffectiveHandshakeTimeout, runCancellation.Token);
            if (ready is null || ready.Kind != WorkerProtocolConstants.ReadyKind)
            {
                throw new WorkerProtocolException(
                    "worker.handshake_failed",
                    "Runner did not announce readiness. Worker Runner 未宣布就绪。");
            }

            await writer.WriteAsync(WorkerMessage.Create(WorkerProtocolConstants.HandshakeKind), runCancellation.Token);
            var accepted = await WorkerProtocolCodec.ReadAsync(
                stdout,
                line => RecordDiagnostic(request.RunId, "runner.stdout_noise", line),
                runCancellation.Token).AsTask()
                .WaitAsync(_launchOptions.EffectiveHandshakeTimeout, runCancellation.Token);
            if (accepted is null || accepted.Kind != WorkerProtocolConstants.HandshakeAcceptedKind)
            {
                throw new WorkerProtocolException(
                    "worker.protocol_mismatch",
                    "Runner rejected the worker protocol handshake. Worker Runner 拒绝了 Worker 协议握手。");
            }

            await writer.WriteAsync(
                WorkerMessage.Create(WorkerProtocolConstants.RunKind, WorkerProtocolCodec.SerializePayload(request), request.RunId, request.Deadline),
                runCancellation.Token);

            var session = new DebugRunSession(
                this,
                process,
                stdout,
                writer,
                stderrTask,
                request,
                publishEvent,
                deadlineCancellation,
                runCancellation);
            session.Start();
            return session;
        }
        catch
        {
            runCancellation.Cancel();
            deadlineCancellation.Dispose();
            runCancellation.Dispose();
            await writer.DisposeAsync();
            stdout.Dispose();
            if (!process.HasExited)
                TerminateProcessTree(process);
            process.Dispose();
            throw;
        }
    }

    public sealed class DebugRunSession : IAsyncDisposable
    {
        private readonly WorkerSupervisor _supervisor;
        private readonly Process _process;
        private readonly StreamReader _stdout;
        private readonly WorkerMessageWriter _writer;
        private readonly Task<string> _stderrTask;
        private readonly WorkerRunRequestDto _request;
        private readonly Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> _publishEvent;
        private readonly CancellationTokenSource _deadlineCancellation;
        private readonly CancellationTokenSource _runCancellation;
        private readonly SemaphoreSlim _commandGate = new(1, 1);
        private Task<WorkerRunResultDto>? _completion;
        private long _lastCommandSequence;
        private int _disposed;

        internal DebugRunSession(
            WorkerSupervisor supervisor,
            Process process,
            StreamReader stdout,
            WorkerMessageWriter writer,
            Task<string> stderrTask,
            WorkerRunRequestDto request,
            Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent,
            CancellationTokenSource deadlineCancellation,
            CancellationTokenSource runCancellation)
        {
            _supervisor = supervisor;
            _process = process;
            _stdout = stdout;
            _writer = writer;
            _stderrTask = stderrTask;
            _request = request;
            _publishEvent = publishEvent;
            _deadlineCancellation = deadlineCancellation;
            _runCancellation = runCancellation;
        }

        public Guid RunId => _request.RunId;

        public Guid DebugSessionId => _request.Debug!.DebugSessionId;

        public Task<WorkerRunResultDto> Completion
            => _completion ?? throw new InvalidOperationException(
                "The debug session has not started monitoring. 调试会话尚未开始监控。");

        internal void Start() => _completion = MonitorAsync();

        public Task ContinueAsync(long commandSequence, CancellationToken cancellationToken = default)
            => SendCommandAsync(WorkerProtocolConstants.DebugContinueKind, commandSequence, cancellationToken);

        public Task StepAsync(long commandSequence, CancellationToken cancellationToken = default)
            => SendCommandAsync(WorkerProtocolConstants.DebugStepKind, commandSequence, cancellationToken);

        public Task StopAsync(long commandSequence, CancellationToken cancellationToken = default)
            => SendCommandAsync(
                WorkerProtocolConstants.DebugStopKind,
                commandSequence,
                cancellationToken,
                cancelRunAfterCommand: true);

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            if (!Completion.IsCompleted)
            {
                try
                {
                    _runCancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
            try
            {
                await Completion.ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Dispose();
            }
        }

        private async Task SendCommandAsync(
            string kind,
            long commandSequence,
            CancellationToken cancellationToken,
            bool cancelRunAfterCommand = false)
        {
            if (commandSequence < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(commandSequence),
                    "The debug command sequence must be positive. 调试命令序号必须为正数。");
            }
            if (Completion.IsCompleted)
            {
                throw new InvalidOperationException(
                    "The debug run is already terminal. 调试运行已到达终态。");
            }

            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            var shouldCancelRun = false;
            try
            {
                if (commandSequence <= _lastCommandSequence)
                {
                    throw new InvalidOperationException(
                        "The debug command sequence must be strictly increasing. 调试命令序号必须严格递增。");
                }

                // Stop is a run-lifecycle command, not merely a gate release.
                // A global Flipflop can be waiting for its next trigger without
                // any paused gate, so the supervisor must own the final
                // cancellation and enforce its grace-period process cleanup.
                // Stop 是运行生命周期命令，不只是闸门放行。全局 Flipflop 可能正在等待
                // 下一次触发而没有暂停闸门，因此 Supervisor 必须负责最终取消并执行宽限期
                // 进程清理。
                shouldCancelRun = cancelRunAfterCommand;
                var command = new WorkerDebugCommandDto(
                    WorkerProtocolConstants.Version,
                    RunId,
                    DebugSessionId,
                    commandSequence);
                await _writer.WriteAsync(
                    WorkerMessage.Create(
                        kind,
                        WorkerProtocolCodec.SerializePayload(command),
                        RunId,
                        sequence: commandSequence),
                    cancellationToken).ConfigureAwait(false);
                _lastCommandSequence = commandSequence;
            }
            finally
            {
                _commandGate.Release();
                if (shouldCancelRun)
                {
                    try
                    {
                        _runCancellation.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
        }

        private async Task<WorkerRunResultDto> MonitorAsync()
        {
            try
            {
                return await _supervisor.MonitorRunAsync(
                    _process,
                    _stdout,
                    _writer,
                    _request,
                    _publishEvent,
                    _deadlineCancellation,
                    _runCancellation.Token).ConfigureAwait(false);
            }
            catch (WorkerProtocolException exception)
            {
                _supervisor.RecordProtocolDiagnostic(RunId, "debug.protocol", exception);
                return Failure(RunId, exception.Code, exception.Message);
            }
            catch (Exception exception)
            {
                _supervisor.RecordDiagnostic(RunId, "debug.supervisor", exception.ToString(), exception);
                return Failure(
                    RunId,
                    "worker.crashed",
                    "The runner exited before returning a valid result. Worker Runner 在返回有效结果前退出。");
            }
            finally
            {
                if (!_process.HasExited)
                    TerminateProcessTree(_process);
                try
                {
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                }

                var stderr = await ReadDiagnosticsAsync(_stderrTask).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(stderr))
                    _supervisor.RecordDiagnostic(RunId, "runner.stderr", stderr);

                await _writer.DisposeAsync().ConfigureAwait(false);
                _stdout.Dispose();
                _process.Dispose();
                _deadlineCancellation.Dispose();
                _runCancellation.Dispose();
            }
        }
    }

    private async Task<WorkerRunResultDto> MonitorRunAsync(
        Process process,
        StreamReader stdout,
        WorkerMessageWriter writer,
        WorkerRunRequestDto request,
        Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent,
        CancellationTokenSource deadlineCancellation,
        CancellationToken callerCancellationToken)
    {
        using var heartbeatCancellation = new CancellationTokenSource();
        var readTask = WorkerProtocolCodec.ReadAsync(
            stdout,
            line => RecordDiagnostic(request.RunId, "runner.stdout_noise", line),
            CancellationToken.None).AsTask();
        var heartbeatTask = Task.Delay(_launchOptions.EffectiveHeartbeatInterval, heartbeatCancellation.Token);
        var callerCancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, callerCancellationToken);
        var deadlineTask = Task.Delay(Timeout.InfiniteTimeSpan, deadlineCancellation.Token);
        long lastSequence = 0;

        while (true)
        {
            var completed = await Task.WhenAny(readTask, heartbeatTask, callerCancellationTask, deadlineTask);
            if (completed == callerCancellationTask || completed == deadlineTask)
            {
                var status = deadlineCancellation.IsCancellationRequested ? FlowRunStatusDto.TimedOut : FlowRunStatusDto.Cancelled;
                var code = status == FlowRunStatusDto.TimedOut ? "worker.timed_out" : "worker.cancelled";
                return await CancelAndReturnAsync(process, stdout, writer, request, publishEvent, status, code, readTask, lastSequence);
            }

            if (completed == heartbeatTask)
            {
                await writer.WriteAsync(WorkerMessage.Create(WorkerProtocolConstants.HeartbeatKind, runId: request.RunId), CancellationToken.None);
                heartbeatTask = Task.Delay(_launchOptions.EffectiveHeartbeatInterval, heartbeatCancellation.Token);
                continue;
            }

            var message = await readTask;
            if (message is null)
                return Failure(request.RunId, "worker.crashed", "The runner closed its protocol stream without a result. Worker Runner 在返回结果前关闭了协议流。");

            var completion = await HandleMessageAsync(message, request, publishEvent, lastSequence, CancellationToken.None);
            if (completion.Result is not null)
                return completion.Result;
            lastSequence = completion.LastSequence;
            readTask = WorkerProtocolCodec.ReadAsync(
                stdout,
                line => RecordDiagnostic(request.RunId, "runner.stdout_noise", line),
                CancellationToken.None).AsTask();
        }
    }

    private async Task<WorkerRunResultDto> CancelAndReturnAsync(
        Process process,
        StreamReader stdout,
        WorkerMessageWriter writer,
        WorkerRunRequestDto request,
        Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent,
        FlowRunStatusDto fallbackStatus,
        string fallbackCode,
        Task<WorkerMessage?>? existingReadTask = null,
        long lastSequence = 0)
    {
        try
        {
            await writer.WriteAsync(
                WorkerMessage.Create(
                    WorkerProtocolConstants.CancelKind,
                    WorkerProtocolCodec.SerializePayload(new WorkerCancelRequestDto(WorkerProtocolConstants.Version, request.RunId, fallbackCode)),
                    request.RunId));
        }
        catch (Exception)
        {
            return await TerminateAndReturnAsync(process, request.RunId, fallbackCode, "Runner could not be reached for cancellation. 无法连接 Worker Runner 以取消运行。", fallbackStatus);
        }

        var readTask = existingReadTask ?? WorkerProtocolCodec.ReadAsync(
            stdout,
            line => RecordDiagnostic(request.RunId, "runner.stdout_noise", line),
            CancellationToken.None).AsTask();
        var graceDeadline = DateTimeOffset.UtcNow + _launchOptions.EffectiveCancellationGracePeriod;
        while (DateTimeOffset.UtcNow < graceDeadline)
        {
            var remaining = graceDeadline - DateTimeOffset.UtcNow;
            var completed = await Task.WhenAny(readTask, Task.Delay(remaining));
            if (completed != readTask)
                break;

            var message = await readTask;
            if (message is null)
                break;

            var completion = await HandleMessageAsync(message, request, publishEvent, lastSequence, CancellationToken.None);
            if (completion.Result is not null)
                return NormalizeCancellationResult(completion.Result, request.RunId, fallbackStatus, fallbackCode);
            lastSequence = completion.LastSequence;
            readTask = WorkerProtocolCodec.ReadAsync(
                stdout,
                line => RecordDiagnostic(request.RunId, "runner.stdout_noise", line),
                CancellationToken.None).AsTask();
        }

        return await TerminateAndReturnAsync(process, request.RunId, fallbackCode, "Runner did not stop before the cancellation grace period elapsed. Worker Runner 在取消宽限期结束前未停止。", fallbackStatus);
    }

    private static WorkerRunResultDto NormalizeCancellationResult(
        WorkerRunResultDto result,
        Guid runId,
        FlowRunStatusDto fallbackStatus,
        string fallbackCode)
    {
        if (fallbackStatus != FlowRunStatusDto.TimedOut || result.Status != FlowRunStatusDto.Cancelled)
            return result;

        return new WorkerRunResultDto(
            WorkerProtocolConstants.Version,
            runId,
            FlowRunStatusDto.TimedOut,
            fallbackCode,
            "The run deadline elapsed. 运行截止时间已到。");
    }

    private static async Task<(WorkerRunResultDto? Result, long LastSequence)> HandleMessageAsync(
        WorkerMessage message,
        WorkerRunRequestDto request,
        Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent,
        long lastSequence,
        CancellationToken cancellationToken)
    {
        if (message.RunId is not null && message.RunId != request.RunId)
            return (Failure(request.RunId, "worker.invalid_message", "Runner returned a message for another run. Worker Runner 返回了属于其他运行实例的消息。"), lastSequence);

        switch (message.Kind)
        {
            case WorkerProtocolConstants.EventKind:
            {
                var workerEvent = WorkerProtocolCodec.DeserializePayload<WorkerEventEnvelopeDto>(message);
                if (workerEvent.RunId != request.RunId || workerEvent.Sequence <= lastSequence)
                    return (Failure(request.RunId, "worker.event_sequence_invalid", "Runner event sequence is not strictly increasing. Worker Runner 的事件序列没有严格递增。"), lastSequence);
                await publishEvent(workerEvent, cancellationToken);
                return (null, workerEvent.Sequence);
            }
            case WorkerProtocolConstants.ResultKind:
            {
                var result = WorkerProtocolCodec.DeserializePayload<WorkerRunResultDto>(message);
                if (result.RunId != request.RunId || result.ProtocolVersion != WorkerProtocolConstants.Version)
                    return (Failure(request.RunId, "worker.invalid_result", "Runner returned an invalid result. Worker Runner 返回了无效结果。"), lastSequence);
                return (result, lastSequence);
            }
            case WorkerProtocolConstants.ErrorKind:
            {
                var error = WorkerProtocolCodec.DeserializePayload<WorkerErrorDto>(message);
                return (Failure(request.RunId, error.Code, error.Message), lastSequence);
            }
            case WorkerProtocolConstants.CancelAcknowledgedKind:
            case WorkerProtocolConstants.HeartbeatAcknowledgedKind:
                return (null, lastSequence);
            default:
                return (Failure(request.RunId, "worker.invalid_message", $"Runner sent unsupported message '{message.Kind}'. Worker Runner 发送了不支持的消息“{message.Kind}”。"), lastSequence);
        }
    }

    private Process StartProcess()
    {
        var startInfo = new ProcessStartInfo(_launchOptions.FileName)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        if (!string.IsNullOrWhiteSpace(_launchOptions.WorkingDirectory))
            startInfo.WorkingDirectory = _launchOptions.WorkingDirectory;
        foreach (var argument in _launchOptions.Arguments)
            startInfo.ArgumentList.Add(argument);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the worker runner process. 无法启动 Worker Runner 进程。");
    }

    private static async Task<WorkerRunResultDto> TerminateAndReturnAsync(Process process, Guid runId, string code, string message, FlowRunStatusDto status)
    {
        if (!process.HasExited)
            TerminateProcessTree(process);
        try
        {
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
        }
        return new WorkerRunResultDto(WorkerProtocolConstants.Version, runId, status, code, message);
    }

    private static void TerminateProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static WorkerRunResultDto Failure(Guid runId, string code, string message)
        => new(WorkerProtocolConstants.Version, runId, FlowRunStatusDto.Failed, code, message);

    private static bool IsAllowedPath(string? candidate, string? allowedRoot)
    {
        // A missing candidate is only valid when the corresponding feature is
        // not configured at all.  Once a service root is configured, the
        // request must carry a path and that path must stay inside the root.
        // 未配置该能力时允许两者均为空；一旦配置根目录，请求必须提供路径且路径不得越界。
        if (string.IsNullOrWhiteSpace(candidate))
            return string.IsNullOrWhiteSpace(allowedRoot);
        if (string.IsNullOrWhiteSpace(allowedRoot))
            return false;

        try
        {
            var root = NormalizePath(allowedRoot);
            var path = NormalizePath(candidate);
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string NormalizePath(string value)
        => Path.GetFullPath(value)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private string? ResolveRunnerPath()
    {
        foreach (var argument in _launchOptions.Arguments.Reverse())
        {
            if (Path.IsPathRooted(argument)
                && (argument.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || argument.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    || !Path.HasExtension(argument)))
            {
                return argument;
            }
        }

        return null;
    }

    private void RecordProtocolDiagnostic(Guid runId, string phase, WorkerProtocolException exception)
    {
        var location = exception.JsonLineNumber is null
            ? string.Empty
            : $" line={exception.JsonLineNumber} byte={exception.JsonBytePositionInLine}";
        var raw = exception.RawMessage is null
            ? string.Empty
            : $" raw={TrimDiagnostic(exception.RawMessage)}";
        RecordDiagnostic(
            runId,
            phase,
            $"code={exception.Code}; message={exception.Message};{location}{raw}",
            exception);
    }

    private void RecordDiagnostic(Guid runId, string phase, string message, Exception? exception = null)
    {
        if (_launchOptions.DiagnosticLogger is null)
            return;

        var exceptionDetails = exception is null
            ? string.Empty
            : $" exception={exception.GetType().FullName}: {exception.InnerException?.Message ?? exception.Message}";
        try
        {
            _launchOptions.DiagnosticLogger(
                $"Worker diagnostic runId={runId:D} phase={phase}; Worker 诊断 runId={runId:D} 阶段={phase}；{TrimDiagnostic(message)}{exceptionDetails}");
        }
        catch
        {
            // Logging must never alter Worker lifecycle or mask the original error.
            // 记录日志失败时不能改变 Worker 生命周期，也不能覆盖原始错误。
        }
    }

    private static string TrimDiagnostic(string value)
    {
        const int maxLength = 4096;
        var normalized = value.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "…";
    }

    private static async Task<string?> ReadDiagnosticsAsync(Task<string> task)
    {
        try
        {
            return await task;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
