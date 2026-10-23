using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;
using SereinFlow.Contracts;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Supervisor;
/// <summary>
/// Configures runner launch behavior.
/// 配置RunnerLaunch行为。
/// </summary>
public sealed record RunnerLaunchOptions(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory = null, TimeSpan? HandshakeTimeout = null, TimeSpan? HeartbeatInterval = null, TimeSpan? CancellationGracePeriod = null, string? AllowedScriptArtifactRoot = null, string? AllowedLibraryPackageRoot = null, Action<string>? DiagnosticLogger = null, TimeSpan? MessageDeliveryTimeout = null, string? AllowedWorkpieceRoot = null)
{
    private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultCancellationGracePeriod = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultMessageDeliveryTimeout = TimeSpan.FromSeconds(5);
    public TimeSpan EffectiveHandshakeTimeout
    {
        get
        {
            return HandshakeTimeout ?? DefaultHandshakeTimeout;
        }
    }

    public TimeSpan EffectiveHeartbeatInterval
    {
        get
        {
            return HeartbeatInterval ?? DefaultHeartbeatInterval;
        }
    }

    public TimeSpan EffectiveCancellationGracePeriod
    {
        get
        {
            return CancellationGracePeriod ?? DefaultCancellationGracePeriod;
        }
    }

    public TimeSpan EffectiveMessageDeliveryTimeout
    {
        get
        {
            return MessageDeliveryTimeout ?? DefaultMessageDeliveryTimeout;
        }
    }
}

/// <summary>
/// Starts worker processes and coordinates run, debug, and message operations.
/// 启动 Worker 进程并协调运行、调试和消息操作。
/// </summary>
public sealed partial class WorkerSupervisor
{
    private readonly RunnerLaunchOptions _launchOptions;
    private readonly ConcurrentDictionary<Guid, WorkerMessageSession> _messageSessions = new();
    public WorkerSupervisor(RunnerLaunchOptions launchOptions)
    {
        if (launchOptions is null)
        {
            throw new ArgumentNullException(nameof(launchOptions), "Worker runner launch options cannot be null. Worker Runner 启动选项不能为空。");
        }

        if (string.IsNullOrWhiteSpace(launchOptions.FileName))
        {
            throw new ArgumentException("Worker runner file name cannot be empty. Worker Runner 文件名不能为空。", nameof(launchOptions));
        }

        _launchOptions = launchOptions;
    }

    public Task<WorkerRunResultDto> RunAsync(WorkerRunRequestDto request, Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent, CancellationToken cancellationToken = default)
    {
        return RunAsync(request, publishEvent, null, cancellationToken);
    }

    /// <summary>
    /// Starts a worker run and forwards its events to the caller.
    /// 启动 Worker 运行并向调用方转发事件。
    /// </summary>
    public async Task<WorkerRunResultDto> RunAsync(WorkerRunRequestDto request, Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent, Func<IReadOnlyList<WorkerEventEnvelopeDto>, CancellationToken, ValueTask>? publishBatch, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request), "The worker request cannot be null. Worker 请求不能为空。");
        }

        if (publishEvent is null)
        {
            throw new ArgumentNullException(nameof(publishEvent), "The worker event publisher cannot be null. Worker 事件发布器不能为空。");
        }

        if (request.ProtocolVersion != WorkerProtocolConstants.Version)
        {
            return Failure(request.RunId, WorkerErrorCodes.ProtocolMismatch, "The requested worker protocol version is not supported. 请求的 Worker 协议版本不受支持。");
        }

        if (!IsAllowedPath(request.ScriptArtifactRootPath, _launchOptions.AllowedScriptArtifactRoot) || !IsAllowedPath(request.LibraryPackageRootPath, _launchOptions.AllowedLibraryPackageRoot) || !IsAllowedPath(request.WorkpieceRootPath, _launchOptions.AllowedWorkpieceRoot))
        {
            return Failure(request.RunId, WorkerErrorCodes.PathOutsideRoot, "Worker artifact paths are outside the configured service roots. Worker 缓存路径超出了服务端允许的根目录。");
        }

        var runnerPath = ResolveRunnerPath();
        if (runnerPath is not null && !File.Exists(runnerPath))
        {
            var message = $"Worker Runner executable was not found at '{runnerPath}'. Worker Runner 可执行文件不存在：'{runnerPath}'。";
            RecordDiagnostic(request.RunId, WorkerDiagnosticCodes.RunnerPath, message);
            return Failure(request.RunId, WorkerErrorCodes.RunnerNotFound, message);
        }

        if (request.Deadline <= DateTimeOffset.UtcNow)
        {
            return new WorkerRunResultDto(WorkerProtocolConstants.Version, request.RunId, FlowRunStatusDto.TimedOut, WorkerErrorCodes.TimedOut, "The run deadline elapsed before the runner started. Worker Runner 启动前运行截止时间已到。");
        }

        using var process = StartProcess();
        await using var transport = new StdioWorkerTransport(process.StandardOutput.BaseStream, process.StandardInput.BaseStream, line =>
        {
            RecordDiagnostic(request.RunId, WorkerDiagnosticCodes.RunnerStdoutNoise, line);
        });
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var deadlineCancellation = new CancellationTokenSource(request.Deadline - DateTimeOffset.UtcNow);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellation.Token);
        var runStarted = false;
        try
        {
            var ready = await transport
                .ReceiveAsync(runCancellation.Token)
                .AsTask()
                .WaitAsync(_launchOptions.EffectiveHandshakeTimeout, runCancellation.Token);
            if (ready is null)
            {
                return await TerminateAndReturnAsync(process, request.RunId, WorkerErrorCodes.Crashed, "Runner closed its protocol stream before announcing readiness. Worker Runner 在宣布就绪前关闭了协议流。", FlowRunStatusDto.Failed);
            }

            if (ready.Kind != WorkerProtocolConstants.ReadyKind)
            {
                return await TerminateAndReturnAsync(process, request.RunId, WorkerErrorCodes.HandshakeFailed, "Runner did not announce readiness. Worker Runner 未宣布就绪。", FlowRunStatusDto.Failed);
            }
            await transport.SendAsync(WorkerMessage.Create(WorkerProtocolConstants.HandshakeKind), runCancellation.Token);
            var accepted = await transport
                .ReceiveAsync(runCancellation.Token)
                .AsTask()
                .WaitAsync(_launchOptions.EffectiveHandshakeTimeout, runCancellation.Token);
            if (accepted is null)
            {
                return await TerminateAndReturnAsync(process, request.RunId, WorkerErrorCodes.Crashed, "Runner closed its protocol stream during the handshake. Worker Runner 在握手期间关闭了协议流。", FlowRunStatusDto.Failed);
            }

            if (accepted.Kind != WorkerProtocolConstants.HandshakeAcceptedKind)
            {
                return await TerminateAndReturnAsync(process, request.RunId, WorkerErrorCodes.ProtocolMismatch, "Runner rejected the worker protocol handshake. Worker Runner 拒绝了 Worker 协议握手。", FlowRunStatusDto.Failed);
            }
            await transport.SendAsync(WorkerMessage.Create(WorkerProtocolConstants.RunKind, WorkerProtocolCodec.SerializePayload(request), request.RunId, request.Deadline), runCancellation.Token);
            runStarted = true;
            var messageSession = new WorkerMessageSession(transport, request, _launchOptions.EffectiveMessageDeliveryTimeout);
            _messageSessions[request.RunId] = messageSession;
            try
            {
                return await MonitorRunAsync(process, transport, request, publishEvent, deadlineCancellation, cancellationToken, messageSession, publishBatch);
            }
            finally
            {
                var keyValuePair = new KeyValuePair<Guid, WorkerMessageSession>(request.RunId, messageSession);
                _messageSessions.TryRemove(keyValuePair);
                messageSession.Complete();
            }
        }
        catch (OperationCanceledException)
        {
            var timedOut = deadlineCancellation.IsCancellationRequested;
            if (!runStarted)
            {
                return await TerminateAndReturnAsync(process, request.RunId, timedOut ? WorkerErrorCodes.TimedOut : WorkerErrorCodes.Cancelled, timedOut ? "The run deadline elapsed before the Worker run started. Worker 运行在启动前已达到截止时间。" : "The Worker run was cancelled before it started. Worker 运行在启动前已取消。", timedOut ? FlowRunStatusDto.TimedOut : FlowRunStatusDto.Cancelled);
            }
            return await CancelAndReturnAsync(process, transport, request, publishEvent, timedOut ? FlowRunStatusDto.TimedOut : FlowRunStatusDto.Cancelled, timedOut ? WorkerErrorCodes.TimedOut : WorkerErrorCodes.Cancelled);
        }
        catch (WorkerProtocolException exception)
        {
            RecordProtocolDiagnostic(request.RunId, "protocol", exception);
            return await TerminateAndReturnAsync(process, request.RunId, exception.Code, exception.Message, FlowRunStatusDto.Failed);
        }
        catch (Exception exception)
        {
            var exceptionText = exception.ToString();
            RecordDiagnostic(request.RunId, "supervisor", exceptionText, exception);
            return await TerminateAndReturnAsync(process, request.RunId, WorkerErrorCodes.Crashed, "The runner exited before returning a valid result. Worker Runner 在返回有效结果前退出。", FlowRunStatusDto.Failed);
        }
        finally
        {
            if (!process.HasExited)
            {
                TerminateProcessTree(process);
            }

            var stderr = await ReadDiagnosticsAsync(stderrTask);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                RecordDiagnostic(request.RunId, WorkerDiagnosticCodes.RunnerStderr, stderr);
            }
        }
    }

    /// <summary>
    /// Starts a Worker process whose transport remains owned by the supervisor
    /// for the lifetime of the returned debug session. This is deliberately a
    /// distinct API from RunAsync so a caller cannot compete with its command
    /// writer or receive loop.
    /// 启动调试 Worker，并在返回会话的生命周期内由 Supervisor 独占其传输；它与
    /// RunAsync 分开，避免调用方竞争命令写入器或唯一接收循环。
    /// </summary>
    public async Task<DebugRunSession> StartDebugAsync(WorkerRunRequestDto request, Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request), "The worker request cannot be null. Worker 请求不能为空。");
        }

        if (publishEvent is null)
        {
            throw new ArgumentNullException(nameof(publishEvent), "The worker event publisher cannot be null. Worker 事件发布器不能为空。");
        }

        if (request.Debug is null || request.Debug.DebugSessionId == Guid.Empty)
        {
            throw new ArgumentException("A debug request requires a non-empty debug session ID. 调试请求需要非空的调试会话 ID。", nameof(request));
        }

        if (request.ProtocolVersion != WorkerProtocolConstants.Version)
        {
            throw new WorkerProtocolException(WorkerErrorCodes.ProtocolMismatch, "The requested worker protocol version is not supported. 请求的 Worker 协议版本不受支持。");
        }

        if (!IsAllowedPath(request.ScriptArtifactRootPath, _launchOptions.AllowedScriptArtifactRoot) || !IsAllowedPath(request.LibraryPackageRootPath, _launchOptions.AllowedLibraryPackageRoot) || !IsAllowedPath(request.WorkpieceRootPath, _launchOptions.AllowedWorkpieceRoot))
        {
            throw new WorkerProtocolException(WorkerErrorCodes.PathOutsideRoot, "Worker artifact paths are outside the configured service roots. Worker 缓存路径超出了服务端允许的根目录。");
        }

        var runnerPath = ResolveRunnerPath();
        if (runnerPath is not null && !File.Exists(runnerPath))
        {
            throw new FileNotFoundException($"Worker Runner executable was not found at '{runnerPath}'. Worker Runner 可执行文件不存在：'{runnerPath}'。", runnerPath);
        }

        if (request.Deadline <= DateTimeOffset.UtcNow)
        {
            throw new TimeoutException("The run deadline elapsed before the runner started. Worker Runner 启动前运行截止时间已到。");
        }

        var process = StartProcess();
        var transport = new StdioWorkerTransport(process.StandardOutput.BaseStream, process.StandardInput.BaseStream, line =>
        {
            RecordDiagnostic(request.RunId, WorkerDiagnosticCodes.RunnerStdoutNoise, line);
        });
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var deadlineCancellation = new CancellationTokenSource(request.Deadline - DateTimeOffset.UtcNow);
        var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellation.Token);
        try
        {
            var ready = await transport
                .ReceiveAsync(runCancellation.Token)
                .AsTask()
                .WaitAsync(_launchOptions.EffectiveHandshakeTimeout, runCancellation.Token);
            if (ready is null || ready.Kind != WorkerProtocolConstants.ReadyKind)
            {
                throw new WorkerProtocolException(WorkerErrorCodes.HandshakeFailed, "Runner did not announce readiness. Worker Runner 未宣布就绪。");
            }
            await transport.SendAsync(WorkerMessage.Create(WorkerProtocolConstants.HandshakeKind), runCancellation.Token);
            var accepted = await transport
                .ReceiveAsync(runCancellation.Token)
                .AsTask()
                .WaitAsync(_launchOptions.EffectiveHandshakeTimeout, runCancellation.Token);
            if (accepted is null || accepted.Kind != WorkerProtocolConstants.HandshakeAcceptedKind)
            {
                throw new WorkerProtocolException(WorkerErrorCodes.ProtocolMismatch, "Runner rejected the worker protocol handshake. Worker Runner 拒绝了 Worker 协议握手。");
            }
            await transport.SendAsync(WorkerMessage.Create(WorkerProtocolConstants.RunKind, WorkerProtocolCodec.SerializePayload(request), request.RunId, request.Deadline), runCancellation.Token);
            var session = new DebugRunSession(this, process, transport, stderrTask, request, publishEvent, deadlineCancellation, runCancellation);
            _messageSessions[request.RunId] = session.MessageSession;
            session.Start();
            return session;
        }
        catch
        {
            runCancellation.Cancel();
            deadlineCancellation.Dispose();
            runCancellation.Dispose();
            await transport.CloseAsync(CancellationToken.None);
            if (!process.HasExited)
            {
                TerminateProcessTree(process);
            }

            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Delivers a JSON message to an explicitly registered endpoint in an active Worker run.
    /// 向活动 Worker 运行中已显式注册的入口投递 JSON 消息。
    /// </summary>
    public Task<WorkerMessageDeliveryResponseDto> DeliverMessageAsync(WorkerMessageDeliveryDto delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        return _messageSessions.TryGetValue(delivery.RunId, out var session) ? session.DeliverAsync(delivery, cancellationToken) : RunNotFoundResponse();
        Task<WorkerMessageDeliveryResponseDto> RunNotFoundResponse()
        {
            var workerMessageDeliveryResponseDto = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.NotFound, WorkerProtocolConstants.Version, delivery.RunId, delivery.MessageId, delivery.Topic, WorkerErrorCodes.NotFound, "The Worker run is not active. Worker 运行当前不活动。");
            return Task.FromResult(workerMessageDeliveryResponseDto);
        }
    }

    public sealed class DebugRunSession : IAsyncDisposable
    {
        private readonly WorkerSupervisor _supervisor;
        private readonly Process _process;
        private readonly IWorkerTransport _transport;
        internal WorkerMessageSession MessageSession { get; }

        private readonly Task<string> _stderrTask;
        private readonly WorkerRunRequestDto _request;
        private readonly Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> _publishEvent;
        private readonly CancellationTokenSource _deadlineCancellation;
        private readonly CancellationTokenSource _runCancellation;
        private readonly SemaphoreSlim _commandGate = new(1, 1);
        private Task<WorkerRunResultDto>? _completion;
        private long _lastCommandSequence;
        private int _disposed;
        internal DebugRunSession(WorkerSupervisor supervisor, Process process, IWorkerTransport transport, Task<string> stderrTask, WorkerRunRequestDto request, Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent, CancellationTokenSource deadlineCancellation, CancellationTokenSource runCancellation)
        {
            _supervisor = supervisor;
            _process = process;
            _transport = transport;
            MessageSession = new WorkerMessageSession(transport, request, supervisor._launchOptions.EffectiveMessageDeliveryTimeout);
            _stderrTask = stderrTask;
            _request = request;
            _publishEvent = publishEvent;
            _deadlineCancellation = deadlineCancellation;
            _runCancellation = runCancellation;
        }

        public Guid RunId => _request.RunId;
        public Guid DebugSessionId => _request.Debug!.DebugSessionId;

        public Task<WorkerRunResultDto> Completion
        {
            get
            {
                return _completion ?? throw new InvalidOperationException("The debug session has not started monitoring. 调试会话尚未开始监控。");
            }
        }

        internal void Start()
        {
            _completion = MonitorAsync();
        }

        public Task ContinueAsync(long commandSequence, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(WorkerProtocolConstants.DebugContinueKind, commandSequence, cancellationToken);
        }

        public Task StepAsync(long commandSequence, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(WorkerProtocolConstants.DebugStepKind, commandSequence, cancellationToken);
        }

        public Task StopAsync(long commandSequence, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(WorkerProtocolConstants.DebugStopKind, commandSequence, cancellationToken, cancelRunAfterCommand: true);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

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

        private async Task SendCommandAsync(string kind, long commandSequence, CancellationToken cancellationToken, bool cancelRunAfterCommand = false)
        {
            if (commandSequence < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(commandSequence), "The debug command sequence must be positive. 调试命令序号必须为正数。");
            }

            if (Completion.IsCompleted)
            {
                throw new InvalidOperationException("The debug run is already terminal. 调试运行已到达终态。");
            }
            await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            var shouldCancelRun = false;
            try
            {
                if (commandSequence <= _lastCommandSequence)
                {
                    throw new InvalidOperationException("The debug command sequence must be strictly increasing. 调试命令序号必须严格递增。");
                }

                // Stop is a run-lifecycle command, not merely a gate release.
                // A global Flipflop can be waiting for its next trigger without
                // any paused gate, so the supervisor must own the final
                // cancellation and enforce its grace-period process cleanup.
                // Stop 是运行生命周期命令，不只是闸门放行。全局 Flipflop 可能正在等待
                // 下一次触发而没有暂停闸门，因此 Supervisor 必须负责最终取消并执行宽限期
                // 进程清理。
                shouldCancelRun = cancelRunAfterCommand;
                var command = new WorkerDebugCommandDto(WorkerProtocolConstants.Version, RunId, DebugSessionId, commandSequence);
                var message = WorkerMessage.Create(kind, WorkerProtocolCodec.SerializePayload(command), RunId, sequence: commandSequence);
                await _transport.SendAsync(message, cancellationToken).ConfigureAwait(false);
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
                return await _supervisor.MonitorRunAsync(_process, _transport, _request, _publishEvent, _deadlineCancellation, _runCancellation.Token, MessageSession).ConfigureAwait(false);
            }
            catch (WorkerProtocolException exception)
            {
                _supervisor.RecordProtocolDiagnostic(RunId, WorkerDiagnosticCodes.DebugProtocol, exception);
                return Failure(RunId, exception.Code, exception.Message);
            }
            catch (Exception exception)
            {
                var message = exception.ToString();
                _supervisor.RecordDiagnostic(RunId, DebugErrorCodes.Supervisor, message, exception);
                return Failure(RunId, WorkerErrorCodes.Crashed, "The runner exited before returning a valid result. Worker Runner 在返回有效结果前退出。");
            }
            finally
            {
                if (!_process.HasExited)
                {
                    TerminateProcessTree(_process);
                }

                try
                {
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                }
                var stderr = await ReadDiagnosticsAsync(_stderrTask).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    _supervisor.RecordDiagnostic(RunId, WorkerDiagnosticCodes.RunnerStderr, stderr);
                }
                await _transport.CloseAsync().ConfigureAwait(false);
                var keyValuePair = new KeyValuePair<Guid, WorkerMessageSession>(RunId, MessageSession);
                _supervisor._messageSessions.TryRemove(keyValuePair);
                MessageSession.Complete();
                _process.Dispose();
                _deadlineCancellation.Dispose();
                _runCancellation.Dispose();
            }
        }
    }

    private async Task<WorkerRunResultDto> MonitorRunAsync(Process process, IWorkerTransport transport, WorkerRunRequestDto request, Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent, CancellationTokenSource deadlineCancellation, CancellationToken callerCancellationToken, WorkerMessageSession? messageSession = null, Func<IReadOnlyList<WorkerEventEnvelopeDto>, CancellationToken, ValueTask>? publishBatch = null)
    {
        await using var dispatcher = new WorkerEventDispatcher(publishBatch ?? PublishBatchAsync);
        async ValueTask PublishBatchAsync(IReadOnlyList<WorkerEventEnvelopeDto> batch, CancellationToken token)
        {
            foreach (var item in batch)
                await publishEvent(item, token);
        }

        using var heartbeatCancellation = new CancellationTokenSource();
        var readTask = transport.ReceiveAsync(CancellationToken.None).AsTask();
        var heartbeatTask = Task.Delay(_launchOptions.EffectiveHeartbeatInterval, heartbeatCancellation.Token);
        var callerCancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, callerCancellationToken);
        var deadlineTask = Task.Delay(Timeout.InfiniteTimeSpan, deadlineCancellation.Token);
        long lastSequence = 0;
        WorkerTransportException? heartbeatFailure = null;
        while (true)
        {
            if (dispatcher.Completion.IsFaulted)
            {
                await dispatcher.Completion;
            }

            var completed = await Task.WhenAny(readTask, heartbeatTask, callerCancellationTask, deadlineTask, dispatcher.Completion);
            if (completed == dispatcher.Completion)
            {
                await dispatcher.Completion;
            }

            if (completed == callerCancellationTask || completed == deadlineTask)
            {
                var status = deadlineCancellation.IsCancellationRequested ? FlowRunStatusDto.TimedOut : FlowRunStatusDto.Cancelled;
                var code = status == FlowRunStatusDto.TimedOut ? WorkerErrorCodes.TimedOut : WorkerErrorCodes.Cancelled;
                var cancelAndReturn = await CancelAndReturnAsync(process, transport, request, dispatcher.PublishAsync, status, code, readTask, lastSequence, messageSession);
                await dispatcher.CompleteAsync();
                return cancelAndReturn;
            }

            if (completed == heartbeatTask)
            {
                if (heartbeatFailure is not null)
                {
                    RecordDiagnostic(request.RunId, "heartbeat", heartbeatFailure.Message, heartbeatFailure);
                    await dispatcher.CompleteAsync();
                    return Failure(request.RunId, WorkerErrorCodes.Crashed, "The runner did not return a result after its input stream closed. Worker Runner 在输入流关闭后未返回结果。");
                }

                var workerMessage = WorkerMessage.Create(WorkerProtocolConstants.HeartbeatKind, runId: request.RunId);
                try
                {
                    using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken, deadlineCancellation.Token);
                    sendCancellation.CancelAfter(_launchOptions.EffectiveCancellationGracePeriod);
                    await transport.SendAsync(workerMessage, sendCancellation.Token);
                }
                catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested || deadlineCancellation.IsCancellationRequested)
                {
                    continue;
                }
                catch (OperationCanceledException)
                {
                    await dispatcher.CompleteAsync();
                    return Failure(request.RunId, WorkerErrorCodes.Crashed, "The Worker control stream stopped accepting messages. Worker 控制流停止接收消息。");
                }
                catch (WorkerTransportException exception) when (exception.Code == WorkerTransportErrorCodes.SendFailed)
                {
                    // The runner can close stdin after flushing its result while
                    // stdout still contains events. Drain that stream before
                    // declaring a crash, with a bounded grace period.
                    // Runner 可能在刷新结果后关闭 stdin，此时 stdout 中仍有事件；在有限宽限期内读完该流，再判断是否崩溃。
                    heartbeatFailure = exception;
                    heartbeatTask = Task.Delay(_launchOptions.EffectiveCancellationGracePeriod, heartbeatCancellation.Token);
                    continue;
                }

                heartbeatTask = Task.Delay(_launchOptions.EffectiveHeartbeatInterval, heartbeatCancellation.Token);
                continue;
            }

            var message = await readTask;
            if (message is null)
            {
                await dispatcher.CompleteAsync();
                return Failure(request.RunId, WorkerErrorCodes.Crashed, "The runner closed its protocol stream without a result. Worker Runner 在返回结果前关闭了协议流。");
            }

            if (messageSession?.HandleControlMessage(message) == true)
            {
                readTask = transport.ReceiveAsync(CancellationToken.None).AsTask();
                continue;
            }

            var completion = await HandleMessageAsync(message, request, dispatcher.PublishAsync, lastSequence, CancellationToken.None);
            if (completion.Result is not null)
            {
                await dispatcher.CompleteAsync();
                return completion.Result;
            }

            lastSequence = completion.LastSequence;
            readTask = transport.ReceiveAsync(CancellationToken.None).AsTask();
        }
    }

    private async Task<WorkerRunResultDto> CancelAndReturnAsync(Process process, IWorkerTransport transport, WorkerRunRequestDto request, Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent, FlowRunStatusDto fallbackStatus, string fallbackCode, Task<WorkerMessage?>? existingReadTask = null, long lastSequence = 0, WorkerMessageSession? messageSession = null)
    {
        var graceDeadline = DateTimeOffset.UtcNow + _launchOptions.EffectiveCancellationGracePeriod;
        try
        {
            var workerCancelRequestDto = new WorkerCancelRequestDto(WorkerProtocolConstants.Version, request.RunId, fallbackCode);
            var workerMessage = WorkerMessage.Create(WorkerProtocolConstants.CancelKind, WorkerProtocolCodec.SerializePayload(workerCancelRequestDto), request.RunId);
            using var sendCancellation = new CancellationTokenSource(_launchOptions.EffectiveCancellationGracePeriod);
            await transport.SendAsync(workerMessage, sendCancellation.Token);
        }
        catch (Exception)
        {
            return await TerminateAndReturnAsync(process, request.RunId, fallbackCode, "Runner could not be reached for cancellation. 无法连接 Worker Runner 以取消运行。", fallbackStatus);
        }

        var readTask = existingReadTask ?? transport.ReceiveAsync(CancellationToken.None).AsTask();
        while (DateTimeOffset.UtcNow < graceDeadline)
        {
            var remaining = graceDeadline - DateTimeOffset.UtcNow;
            var completed = await Task.WhenAny(readTask, Task.Delay(remaining));
            if (completed != readTask)
            {
                break;
            }

            var message = await readTask;
            if (message is null)
            {
                break;
            }

            if (messageSession?.HandleControlMessage(message) == true)
            {
                readTask = transport.ReceiveAsync(CancellationToken.None).AsTask();
                continue;
            }

            var completion = await HandleMessageAsync(message, request, publishEvent, lastSequence, CancellationToken.None);
            if (completion.Result is not null)
            {
                return NormalizeCancellationResult(completion.Result, request.RunId, fallbackStatus, fallbackCode);
            }

            lastSequence = completion.LastSequence;
            readTask = transport.ReceiveAsync(CancellationToken.None).AsTask();
        }
        return await TerminateAndReturnAsync(process, request.RunId, fallbackCode, "Runner did not stop before the cancellation grace period elapsed. Worker Runner 在取消宽限期结束前未停止。", fallbackStatus);
    }

    private static WorkerRunResultDto NormalizeCancellationResult(WorkerRunResultDto result, Guid runId, FlowRunStatusDto fallbackStatus, string fallbackCode)
    {
        if (fallbackStatus != FlowRunStatusDto.TimedOut || result.Status != FlowRunStatusDto.Cancelled)
        {
            return result;
        }
        return new WorkerRunResultDto(WorkerProtocolConstants.Version, runId, FlowRunStatusDto.TimedOut, fallbackCode, "The run deadline elapsed. 运行截止时间已到。", result.FinalVariables);
    }

    private static async Task<(WorkerRunResultDto? Result, long LastSequence)> HandleMessageAsync(WorkerMessage message, WorkerRunRequestDto request, Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publishEvent, long lastSequence, CancellationToken cancellationToken)
    {
        if (message.RunId is not null && message.RunId != request.RunId)
        {
            return (Failure(request.RunId, WorkerErrorCodes.InvalidMessage, "Runner returned a message for another run. Worker Runner 返回了属于其他运行实例的消息。"), lastSequence);
        }

        switch (message.Kind)
        {
            case WorkerProtocolConstants.EventKind:
                {
                    var workerEvent = WorkerProtocolCodec.DeserializePayload<WorkerEventEnvelopeDto>(message);
                    if (workerEvent.RunId != request.RunId || workerEvent.Sequence <= lastSequence)
                    {
                        return (Failure(request.RunId, WorkerErrorCodes.EventSequenceInvalid, "Runner event sequence is not strictly increasing. Worker Runner 的事件序列没有严格递增。"), lastSequence);
                    }

                    await publishEvent(workerEvent, cancellationToken);
                    return (null, workerEvent.Sequence);
                }

            case WorkerProtocolConstants.ResultKind:
                {
                    var result = WorkerProtocolCodec.DeserializePayload<WorkerRunResultDto>(message);
                    if (result.RunId != request.RunId || result.ProtocolVersion != WorkerProtocolConstants.Version)
                    {
                        return (Failure(request.RunId, WorkerErrorCodes.InvalidResult, "Runner returned an invalid result. Worker Runner 返回了无效结果。"), lastSequence);
                    }

                    return (result, lastSequence);
                }

            case WorkerProtocolConstants.ErrorKind:
                {
                    var error = WorkerProtocolCodec.DeserializePayload<WorkerErrorDto>(message);
                    return (Failure(request.RunId, error.Code, error.Message) with { FinalVariables = error.FinalVariables }, lastSequence);
                }

            case WorkerProtocolConstants.CancelAcknowledgedKind:
            case WorkerProtocolConstants.HeartbeatAcknowledgedKind:
            case WorkerProtocolConstants.ReadyKind:
            case WorkerProtocolConstants.HandshakeKind:
            case WorkerProtocolConstants.HandshakeAcceptedKind:
                return (null, lastSequence);
            default:
                return (Failure(request.RunId, WorkerErrorCodes.InvalidMessage, $"Runner sent unsupported message '{message.Kind}'. Worker Runner 发送了不支持的消息“{message.Kind}”。"), lastSequence);
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
        {
            startInfo.WorkingDirectory = _launchOptions.WorkingDirectory;
        }

        foreach (var argument in _launchOptions.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the worker runner process. 无法启动 Worker Runner 进程。");
    }

    private static async Task<WorkerRunResultDto> TerminateAndReturnAsync(Process process, Guid runId, string code, string message, FlowRunStatusDto status)
    {
        if (!process.HasExited)
        {
            TerminateProcessTree(process);
        }

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
    {
        return new(WorkerProtocolConstants.Version, runId, FlowRunStatusDto.Failed, code, message);
    }

    private static bool IsAllowedPath(string? candidate, string? allowedRoot)
    {
        // A missing candidate is only valid when the corresponding feature is
        // not configured at all.  Once a service root is configured, the
        // request must carry a path and that path must stay inside the root.
        // 未配置该能力时允许两者均为空；一旦配置根目录，请求必须提供路径且路径不得越界。
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.IsNullOrWhiteSpace(allowedRoot);
        }

        if (string.IsNullOrWhiteSpace(allowedRoot))
        {
            return false;
        }

        try
        {
            var root = NormalizePath(allowedRoot);
            var path = NormalizePath(candidate);
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
    {
        return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private string? ResolveRunnerPath()
    {
        foreach (var argument in _launchOptions.Arguments.Reverse())
        {
            if (Path.IsPathRooted(argument) && (argument.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || argument.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !Path.HasExtension(argument)))
            {
                return argument;
            }
        }

        return null;
    }

    private void RecordProtocolDiagnostic(Guid runId, string phase, WorkerProtocolException exception)
    {
        var location = exception.JsonLineNumber is null ? string.Empty : $" line={exception.JsonLineNumber} byte={exception.JsonBytePositionInLine}";
        var raw = exception.RawMessage is null ? string.Empty : $" raw={TrimDiagnostic(exception.RawMessage)}";
        RecordDiagnostic(runId, phase, $"code={exception.Code}; message={exception.Message};{location}{raw}", exception);
    }

    private void RecordDiagnostic(Guid runId, string phase, string message, Exception? exception = null)
    {
        if (_launchOptions.DiagnosticLogger is null)
        {
            return;
        }

        var exceptionDetails = exception is null ? string.Empty : $" exception={exception.GetType().FullName}: {exception.InnerException?.Message ?? exception.Message}";
        try
        {
            var diagnosticMessage = $"Worker diagnostic runId={runId:D} phase={phase}; Worker 诊断 runId={runId:D} 阶段={phase}；{TrimDiagnostic(message)}{exceptionDetails}";
            _launchOptions.DiagnosticLogger(diagnosticMessage);
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

internal sealed partial class WorkerMessageSession
{
    private readonly IWorkerTransport _transport;
    private readonly WorkerRunRequestDto _request;
    private readonly TimeSpan _deliveryTimeout;
    private readonly ConcurrentDictionary<MessageEndpointKey, WorkerMessageEndpointDto> _endpoints = new();
    private readonly ConcurrentDictionary<string, PendingDelivery> _pending = new(StringComparer.Ordinal);
    private int _completed;
    public WorkerMessageSession(IWorkerTransport transport, WorkerRunRequestDto request, TimeSpan deliveryTimeout)
    {
        _transport = transport;
        _request = request;
        _deliveryTimeout = deliveryTimeout;
    }

    public async Task<WorkerMessageDeliveryResponseDto> DeliverAsync(WorkerMessageDeliveryDto delivery, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _completed) == 1)
        {
            return NotFound(delivery);
        }

        if (delivery.ProtocolVersion != WorkerProtocolConstants.Version)
        {
            return Rejected(delivery, MessageErrorCodes.ProtocolMismatch, "The message protocol version is not supported. 消息协议版本不受支持。");
        }

        if (delivery.RunId != _request.RunId)
        {
            return Rejected(delivery, MessageErrorCodes.RunMismatch, "The message belongs to another Worker run. 消息属于其他 Worker 运行。");
        }

        if (delivery.MessageId == Guid.Empty)
        {
            return Rejected(delivery, MessageErrorCodes.IdRequired, "MessageId is required. MessageId 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(delivery.Topic))
        {
            return Rejected(delivery, MessageErrorCodes.TopicRequired, "Message topic is required. 消息主题不能为空。");
        }

        if (!Enum.IsDefined(delivery.ChannelKind))
        {
            return Rejected(delivery, MessageErrorCodes.ChannelInvalid, "The message channel kind is not supported. 消息通道类型不受支持。");
        }

        if (delivery.SerializationMode != WorkerMessageSerializationModeDto.Json)
        {
            return Rejected(delivery, MessageErrorCodes.ExternalJsonRequired, "External ingress only accepts JSON messages. 外部入口只接受 JSON 消息。");
        }

        var topic = delivery.Topic.Trim();
        var key = new MessageEndpointKey(delivery.ChannelKind, topic);
        if (!_endpoints.TryGetValue(key, out var endpoint))
        {
            return new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.NotReady, WorkerProtocolConstants.Version, delivery.RunId, delivery.MessageId, delivery.Topic, MessageErrorCodes.EndpointNotReady, "The message endpoint is not registered. 消息入口尚未注册。");
        }

        if (!endpoint.ExternalIngress)
        {
            return Rejected(delivery, MessageErrorCodes.EndpointForbidden, "The message endpoint is not open to external ingress. 消息入口未开放外部投递。");
        }
        var requestId = Guid.NewGuid().ToString("N");
        var taskCompletionSource = new TaskCompletionSource<WorkerMessageDeliveryResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new PendingDelivery(delivery, taskCompletionSource);
        if (!_pending.TryAdd(requestId, pending))
        {
            return Rejected(delivery, MessageErrorCodes.CorrelationConflict, "The message correlation ID was already in use. 消息关联 ID 已被使用。");
        }

        try
        {
            var message = WorkerMessage.Create(WorkerProtocolConstants.MessageDeliverKind, WorkerProtocolCodec.SerializePayload(delivery), delivery.RunId, requestId: requestId);
            await _transport.SendAsync(message, cancellationToken).ConfigureAwait(false);
            return await pending.Completion.Task.WaitAsync(_deliveryTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.TimedOut, WorkerProtocolConstants.Version, delivery.RunId, delivery.MessageId, delivery.Topic, MessageErrorCodes.DeliveryTimeout, "The Worker did not acknowledge the message before the delivery timeout. Worker 未在投递超时前确认消息。");
        }
        catch (WorkerTransportException exception)
        {
            return new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.NotFound, WorkerProtocolConstants.Version, delivery.RunId, delivery.MessageId, delivery.Topic, exception.Code, "The Worker transport is no longer available. Worker 传输已不可用。");
        }
        catch (IOException exception)
        {
            return new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.NotFound, WorkerProtocolConstants.Version, delivery.RunId, delivery.MessageId, delivery.Topic, WorkerErrorCodes.TransportClosed, exception.Message);
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    public bool HandleControlMessage(WorkerMessage message)
    {
        if (message.RunId != _request.RunId)
        {
            throw new WorkerProtocolException(MessageErrorCodes.RunMismatch, "The Worker message belongs to another run. Worker 消息属于其他运行实例。");
        }

        switch (message.Kind)
        {
            case WorkerProtocolConstants.VariableResponseKind:
                return HandleVariableResponse(message);
            case WorkerProtocolConstants.MessageRegisterKind:
                {
                    var endpoint = WorkerProtocolCodec.DeserializePayload<WorkerMessageEndpointDto>(message);
                    ValidateEndpoint(endpoint);
                    var topic = endpoint.Topic.Trim();
                    var indexKey = new MessageEndpointKey(endpoint.ChannelKind, topic);
                    _endpoints[indexKey] = endpoint with
                    {
                        Topic = endpoint.Topic.Trim()
                    };
                    return true;
                }

            case WorkerProtocolConstants.MessageUnregisterKind:
                {
                    var endpoint = WorkerProtocolCodec.DeserializePayload<WorkerMessageEndpointDto>(message);
                    ValidateEndpoint(endpoint);
                    var endpointTopic = endpoint.Topic.Trim();
                    var messageEndpointKey = new MessageEndpointKey(endpoint.ChannelKind, endpointTopic);
                    _endpoints.TryRemove(messageEndpointKey, out _);
                    return true;
                }

            case WorkerProtocolConstants.MessageAcceptedKind:
                {
                    var accepted = WorkerProtocolCodec.DeserializePayload<WorkerMessageAcceptedDto>(message);
                    ValidateReceipt(accepted.ProtocolVersion, accepted.RunId, accepted.MessageId, accepted.Topic);
                    if (_pending.TryGetValue(message.RequestId, out var pending))
                    {
                        ValidateReceiptMatches(pending.Delivery, accepted.MessageId, accepted.Topic);
                        var workerMessageDeliveryResponseDto = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.Accepted, accepted.ProtocolVersion, accepted.RunId, accepted.MessageId, accepted.Topic, null, "The message was accepted by the Worker. Worker 已接受消息。", accepted.Duplicate);
                        pending.Completion.TrySetResult(workerMessageDeliveryResponseDto);
                    }

                    return true;
                }

            case WorkerProtocolConstants.MessageRejectedKind:
                {
                    var rejected = WorkerProtocolCodec.DeserializePayload<WorkerMessageRejectedDto>(message);
                    ValidateReceipt(rejected.ProtocolVersion, rejected.RunId, rejected.MessageId, rejected.Topic);
                    if (_pending.TryGetValue(message.RequestId, out var pending))
                    {
                        ValidateReceiptMatches(pending.Delivery, rejected.MessageId, rejected.Topic);
                        var rejectedResponse = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.Rejected, rejected.ProtocolVersion, rejected.RunId, rejected.MessageId, rejected.Topic, rejected.Code, rejected.Message);
                        pending.Completion.TrySetResult(rejectedResponse);
                    }

                    return true;
                }

            default:
                return false;
        }
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 1)
        {
            return;
        }

        CompleteVariables();
        foreach (var pending in _pending.Values)
        {
            var workerMessageDeliveryResponseDto = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.NotFound, WorkerProtocolConstants.Version, _request.RunId, Guid.Empty, string.Empty, WorkerErrorCodes.NotFound, "The Worker run is no longer active. Worker 运行已不再活动。");
            pending.Completion.TrySetResult(workerMessageDeliveryResponseDto);
        }

        _pending.Clear();
        _endpoints.Clear();
    }

    private void ValidateEndpoint(WorkerMessageEndpointDto endpoint)
    {
        if (endpoint.ProtocolVersion != WorkerProtocolConstants.Version || endpoint.RunId != _request.RunId || string.IsNullOrWhiteSpace(endpoint.Topic) || !Enum.IsDefined(endpoint.ChannelKind) || !Enum.IsDefined(endpoint.SerializationMode))
        {
            throw new WorkerProtocolException(MessageErrorCodes.EndpointInvalid, "The Worker message endpoint registration is invalid. Worker 消息端点注册无效。");
        }
    }

    private void ValidateReceipt(int protocolVersion, Guid runId, Guid messageId, string topic)
    {
        if (protocolVersion != WorkerProtocolConstants.Version || runId != _request.RunId || messageId == Guid.Empty || string.IsNullOrWhiteSpace(topic))
        {
            throw new WorkerProtocolException(MessageErrorCodes.ReceiptInvalid, "The Worker message receipt is invalid. Worker 消息应答无效。");
        }
    }

    private static void ValidateReceiptMatches(WorkerMessageDeliveryDto delivery, Guid messageId, string topic)
    {
        if (delivery.MessageId != messageId || !DeliveryTopicMatches())
        {
            throw new WorkerProtocolException(MessageErrorCodes.ReceiptMismatch, "The Worker message receipt does not match the delivery request. Worker 消息应答与投递请求不匹配。");
        }

        bool DeliveryTopicMatches()
        {
            var deliveryTopic = delivery.Topic.Trim();
            var requestedTopic = topic.Trim();
            return string.Equals(deliveryTopic, requestedTopic, StringComparison.Ordinal);
        }
    }

    private static WorkerMessageDeliveryResponseDto NotFound(WorkerMessageDeliveryDto delivery)
    {
        return new(WorkerMessageDeliveryStatusDto.NotFound, WorkerProtocolConstants.Version, delivery.RunId, delivery.MessageId, delivery.Topic, WorkerErrorCodes.NotFound, "The Worker run is not active. Worker 运行当前不活动。");
    }

    private static WorkerMessageDeliveryResponseDto Rejected(WorkerMessageDeliveryDto delivery, string code, string message)
    {
        return new(WorkerMessageDeliveryStatusDto.Rejected, WorkerProtocolConstants.Version, delivery.RunId, delivery.MessageId, delivery.Topic, code, message);
    }

    private readonly record struct MessageEndpointKey(WorkerMessageChannelKindDto ChannelKind, string Topic);
    private sealed record PendingDelivery(WorkerMessageDeliveryDto Delivery, TaskCompletionSource<WorkerMessageDeliveryResponseDto> Completion);
}
