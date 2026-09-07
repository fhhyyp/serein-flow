using System.Net.Http;

namespace SereinFlow.Client;

public sealed class SereinFlowClient : ISereinFlowClient, IDisposable, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly SereinFlowHttpClient _transport;

    public SereinFlowClient(SereinFlowClientOptions options)
        : this(CreateHttpClient(options), options, ownsHttpClient: true)
    {
    }

    public SereinFlowClient(HttpClient httpClient, SereinFlowClientOptions options)
        : this(httpClient, options, ownsHttpClient: false)
    {
    }

    private SereinFlowClient(HttpClient httpClient, SereinFlowClientOptions options, bool ownsHttpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;
        _transport = new SereinFlowHttpClient(httpClient, options);
        Flows = new FlowInvocationClient(_transport);
        Runs = new FlowRunClient(_transport);
        Messages = new RunMessageClient(_transport);
        Workpieces = new RunWorkpieceClient(_transport);
    }

    public IFlowInvocationClient Flows { get; }

    public IFlowRunClient Runs { get; }

    public IRunMessageClient Messages { get; }

    public IRunWorkpieceClient Workpieces { get; }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    internal static HttpClient CreateHttpClient(SereinFlowClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validate();
        return new HttpClient { BaseAddress = validated.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
    }
}
