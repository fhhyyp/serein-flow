namespace SereinFlow.Api;
internal static class WorkerLog
{
    public static readonly Action<ILogger, string, Exception?> WorkerDiagnostic = CreateWorkerDiagnosticInitialValue();
    private static Action<ILogger, string, Exception?> CreateWorkerDiagnosticInitialValue()
    {
        var eventId = new EventId(4102, "WorkerDiagnostic");
        Action<ILogger, string, Exception?> workerDiagnosticInitialValue = LoggerMessage.Define<string>(LogLevel.Error, eventId, "{WorkerDiagnostic}");
        return workerDiagnosticInitialValue;
    }
}
