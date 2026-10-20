using SereinFlow.Client;

var sereinFlowClientOptions = new SereinFlowClientOptions
{
    BaseAddress = new Uri("http://127.0.0.1:8188/"),
    ApiKey = Environment.GetEnvironmentVariable("SEREINFLOW_API_KEY"),
};
using var client = new SereinFlowClient(sereinFlowClientOptions);
Console.WriteLine("Configure projectId, flowId, and inputs before invoking a flow.");
