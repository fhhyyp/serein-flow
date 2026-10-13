using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Worker.Runner;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerVariableServiceTests
{
    [Theory]
    [InlineData("int32", "42", typeof(int))]
    [InlineData("int64", "42", typeof(long))]
    [InlineData("double", "1.5", typeof(double))]
    [InlineData("decimal", "1.5", typeof(decimal))]
    [InlineData("boolean", "true", typeof(bool))]
    [InlineData("string", "\"text\"", typeof(string))]
    [InlineData("json", "{\"count\":42}", typeof(JsonElement))]
    public void RemoteWritesStoreTheRequestedClrType(string valueType, string json, Type type)
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        var response = WorkerVariableService.Execute(runId, variables,
            new(WorkerProtocol.Version, runId, "set", "value", JsonSerializer.Deserialize<JsonElement>(json), valueType));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(type, variables.Get("value")!.GetType());
        Assert.Equal(type.FullName, response.Variable!.ClrType);
        Assert.True(response.Variable.ValueAvailable);
    }

    [Fact]
    public void ListIsPagedAndNullIsDifferentFromAMissingVariable()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        variables.Set("b", 2);
        variables.Set<string?>("a", null);
        var response = WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "list", Take: 1));

        Assert.Equal(2, response.Snapshot!.TotalCount);
        Assert.True(response.Snapshot.HasMore);
        var entry = Assert.Single(response.Snapshot.Variables);
        Assert.Equal("a", entry.Name);
        Assert.True(entry.ValueAvailable);
        Assert.Equal(JsonValueKind.Null, entry.Value!.Value.ValueKind);
        var next = WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "list", Offset: 1, Take: 1));
        Assert.Equal("b", Assert.Single(next.Snapshot!.Variables).Name);
        Assert.False(next.Snapshot.HasMore);
        Assert.Equal(404, WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "get", "missing")).StatusCode);
    }

    [Fact]
    public void ComplexObjectsDoNotInvokeTheirGettersOrToString()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        variables.Set("handle", new DangerousObject());
        variables.Set("collection", new Dictionary<string, object?> { ["items"] = new List<int> { 1, 2 } });
        var response = WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "list"));
        Assert.Equal(200, response.StatusCode);
        var handle = Assert.Single(response.Snapshot!.Variables, item => item.Name == "handle");
        Assert.False(handle.ValueAvailable);
        Assert.Equal("unsupported_type", handle.UnavailableReason);
        var collection = Assert.Single(response.Snapshot.Variables, item => item.Name == "collection");
        Assert.Equal(2, collection.Value!.Value.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("int32", "\"42\"", VariableErrorCodes.ValueInvalid)]
    [InlineData("int32", "2147483648", VariableErrorCodes.ValueInvalid)]
    [InlineData("boolean", "null", VariableErrorCodes.ValueInvalid)]
    [InlineData("System.Int32, mscorlib", "42", VariableErrorCodes.TypeInvalid)]
    public void InvalidWritesLeaveTheExistingValueUnchanged(string type, string json, string code)
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        variables.Set("value", 7);
        var response = WorkerVariableService.Execute(runId, variables,
            new(WorkerProtocol.Version, runId, "set", "value", JsonSerializer.Deserialize<JsonElement>(json), type));
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(code, response.Code);
        Assert.Equal(7, variables.Get<int>("value"));
    }

    [Fact]
    public void BoundsAndRunIdentityAreValidated()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        Assert.Equal(400, WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, Guid.NewGuid(), "list")).StatusCode);
        Assert.Equal(400, WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "list", Take: 201)).StatusCode);
        Assert.Equal(400, WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "list", Offset: -1)).StatusCode);
        var large = JsonSerializer.SerializeToElement(new string('中', 16_385));
        Assert.Equal(413, WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "set", "large", large, "string")).StatusCode);
        variables.Set("large", new string('a', 20_000));
        var response = WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "get", "large"));
        Assert.False(response.Variable!.ValueAvailable);
    }

    [Fact]
    public void JsonTextPreservesLargeIntegersAndDecimalPrecision()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        variables.Set("integer", 9_007_199_254_740_993L);
        variables.Set("decimal", 0.1234567890123456789012345678m);
        var result = WorkerVariableService.Execute(runId, variables, new(WorkerProtocol.Version, runId, "list"));
        Assert.Equal("9007199254740993", Assert.Single(result.Snapshot!.Variables, value => value.Name == "integer").ValueJson);
        Assert.Equal("0.1234567890123456789012345678", Assert.Single(result.Snapshot.Variables, value => value.Name == "decimal").ValueJson);
    }

    [Fact]
    public void TableSnapshotsCannotChangeTheTable()
    {
        var variables = new LibraryVariableService();
        variables.Set("count", 1);
        var snapshot = Assert.IsType<Dictionary<string, object?>>(variables.Snapshot());
        snapshot["count"] = 9;
        variables.Set("new", 2);
        Assert.Equal(1, variables.Get<int>("count"));
        Assert.False(snapshot.ContainsKey("new"));
    }

    [Fact]
    public void FinalCapturePreservesSafeValuesAndDoesNotEvaluateComplexObjects()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        variables.Set("integer", 9_007_199_254_740_993L);
        variables.Set("handle", new DangerousObject());
        variables.Set("nonfinite", double.NaN);
        var snapshot = WorkerVariableService.CaptureFinal(runId, variables);
        Assert.True(snapshot.IsFinal);
        Assert.False(snapshot.IsTruncated);
        Assert.False(snapshot.HasMore);
        Assert.Equal(runId, snapshot.RunId);
        Assert.Equal(3, snapshot.TotalCount);
        Assert.Equal("9007199254740993", Assert.Single(snapshot.Variables, value => value.Name == "integer").ValueJson);
        Assert.Equal("unsupported_type", Assert.Single(snapshot.Variables, value => value.Name == "handle").UnavailableReason);
        Assert.Equal("not_json", Assert.Single(snapshot.Variables, value => value.Name == "nonfinite").UnavailableReason);
        variables.Set("integer", 0L);
        Assert.Equal("9007199254740993", Assert.Single(snapshot.Variables, value => value.Name == "integer").ValueJson);
    }

    [Fact]
    public void FinalCaptureFitsTheWorkerProtocolAndMarksOmittedData()
    {
        var variables = new LibraryVariableService();
        for (var i = 0; i < 2_100; i++) variables.Set($"value-{i:D4}-{new string('中', 150)}", new string('a', 8_000));
        variables.Set(new string('a', 257), 1);
        var snapshot = WorkerVariableService.CaptureFinal(Guid.NewGuid(), variables);
        Assert.True(snapshot.IsTruncated);
        Assert.Equal(2_101, snapshot.TotalCount);
        Assert.InRange(snapshot.Variables.Count, 1, WorkerVariableService.MaxFinalVariables);
        Assert.All(snapshot.Variables, variable => Assert.InRange(variable.Name.Length, 1, 256));
        Assert.Contains(snapshot.Variables, variable => variable.UnavailableReason == "snapshot_limit");
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(Worker.Protocol.WorkerProtocolCodec.SerializePayload(snapshot)) < WorkerVariableService.MaxFinalSnapshotBytes);
        var result = new WorkerRunResultDto(WorkerProtocol.Version, snapshot.RunId, FlowRunStatusDto.Succeeded, null, null, snapshot);
        var message = Worker.Protocol.WorkerMessage.Create(Worker.Protocol.WorkerProtocolConstants.ResultKind,
            Worker.Protocol.WorkerProtocolCodec.SerializePayload(result), snapshot.RunId);
        Assert.NotEmpty(Worker.Protocol.WorkerProtocolCodec.Serialize(message));
    }

    private sealed class DangerousObject
    {
        public string Value => throw new InvalidOperationException("Getter must not be invoked.");
        public override string ToString() => throw new InvalidOperationException("ToString must not be invoked.");
    }
}
