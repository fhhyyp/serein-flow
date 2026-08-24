using SereinFlow.Worker.Supervisor;

if (args.Contains("--help", StringComparer.Ordinal))
{
    Console.WriteLine("SereinFlow Worker Supervisor is hosted by the API Worker.Client. Configure a RunnerLaunchOptions instance to use it.");
    return;
}

Console.WriteLine("SereinFlow Worker Supervisor is ready for API-hosted orchestration.");
