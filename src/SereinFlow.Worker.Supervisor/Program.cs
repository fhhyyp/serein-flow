using SereinFlow.Worker.Supervisor;

if (args.Contains("--help", StringComparer.Ordinal))
{
    Console.WriteLine("SereinFlow Worker Supervisor is hosted by the API Worker.Client. Configure a RunnerLaunchOptions instance to use it. SereinFlow Worker Supervisor 由 API Worker.Client 承载，请配置 RunnerLaunchOptions 实例后使用。");
    return;
}

Console.WriteLine("SereinFlow Worker Supervisor is ready for API-hosted orchestration. SereinFlow Worker Supervisor 已准备好接受 API 托管的编排。");
