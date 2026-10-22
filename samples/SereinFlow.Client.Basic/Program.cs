using SereinFlow.Client;

var sereinFlowClientOptions = new SereinFlowClientOptions
{
    BaseAddress = new Uri("http://127.0.0.1:8188/"),
    BearerTokenProvider = _ =>
    {
        var argument1 = Environment.GetEnvironmentVariable("SEREINFLOW_BEARER_TOKEN");
        var value1 = ValueTask.FromResult(argument1);
        return value1;
    },
};
using var client = new SereinFlowClient(sereinFlowClientOptions);
Console.WriteLine("Configure projectId, flowId, and inputs before invoking a flow.");
