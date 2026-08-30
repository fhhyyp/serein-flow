namespace SereinFlow.Api;

internal static class WorkerLog
{
    public static readonly Action<ILogger, string, Exception?> WorkerDiagnostic =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4102, "WorkerDiagnostic"),
            "{WorkerDiagnostic}");
}
