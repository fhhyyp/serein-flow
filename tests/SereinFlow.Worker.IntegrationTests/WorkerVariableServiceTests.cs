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
        var deserializeResult = JsonSerializer.Deserialize<JsonElement>(json);
        WorkerVariableRequestDto evaluatedValue = new(WorkerProtocol.Version, runId, "set", "value", deserializeResult, valueType);
        var response = WorkerVariableService.Execute(runId, variables, evaluatedValue);
        Assert.Equal(200, response.StatusCode);
        var evaluatedValue1 = variables.Get("value")!;
        var getTypeResult = evaluatedValue1.GetType();
        Assert.Equal(type, getTypeResult);
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
        WorkerVariableRequestDto evaluatedValue2 = new(WorkerProtocol.Version, runId, "list", Take: 1);
        var response = WorkerVariableService.Execute(runId, variables, evaluatedValue2);
        Assert.Equal(2, response.Snapshot!.TotalCount);
        Assert.True(response.Snapshot.HasMore);
        var entry = Assert.Single(response.Snapshot.Variables);
        Assert.Equal("a", entry.Name);
        Assert.True(entry.ValueAvailable);
        Assert.Equal(JsonValueKind.Null, entry.Value!.Value.ValueKind);
        WorkerVariableRequestDto evaluatedValue3 = new(WorkerProtocol.Version, runId, "list", Offset: 1, Take: 1);
        var next = WorkerVariableService.Execute(runId, variables, evaluatedValue3);
        var evaluatedValue4 = Assert.Single(next.Snapshot!.Variables).Name;
        Assert.Equal("b", evaluatedValue4);
        Assert.False(next.Snapshot.HasMore);
        WorkerVariableRequestDto evaluatedValue5 = new(WorkerProtocol.Version, runId, "get", "missing");
        var evaluatedValue6 = WorkerVariableService.Execute(runId, variables, evaluatedValue5).StatusCode;
        Assert.Equal(404, evaluatedValue6);
    }

    [Fact]
    public void ComplexObjectsDoNotInvokeTheirGettersOrToString()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        var dangerousObject = new DangerousObject();
        variables.Set("handle", dangerousObject);
        var dictionary = new Dictionary<string, object?>
        {
            ["items"] = new List<int>
            {
                1,
                2
            }
        };
        variables.Set("collection", dictionary);
        WorkerVariableRequestDto evaluatedValue7 = new(WorkerProtocol.Version, runId, "list");
        var response = WorkerVariableService.Execute(runId, variables, evaluatedValue7);
        Assert.Equal(200, response.StatusCode);
        var handle = Assert.Single(response.Snapshot!.Variables, item => item.Name == "handle");
        Assert.False(handle.ValueAvailable);
        Assert.Equal("unsupported_type", handle.UnavailableReason);
        var collection = Assert.Single(response.Snapshot.Variables, item => item.Name == "collection");
        var getPropertyResult = collection.Value!.Value.GetProperty("items");
        var getArrayLengthResult = getPropertyResult.GetArrayLength();
        Assert.Equal(2, getArrayLengthResult);
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
        var deserializeResult8 = JsonSerializer.Deserialize<JsonElement>(json);
        WorkerVariableRequestDto evaluatedValue9 = new(WorkerProtocol.Version, runId, "set", "value", deserializeResult8, type);
        var response = WorkerVariableService.Execute(runId, variables, evaluatedValue9);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(code, response.Code);
        var getResult = variables.Get<int>("value");
        Assert.Equal(7, getResult);
    }

    [Fact]
    public void BoundsAndRunIdentityAreValidated()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        var newGuidResult = Guid.NewGuid();
        WorkerVariableRequestDto evaluatedValue10 = new(WorkerProtocol.Version, newGuidResult, "list");
        var evaluatedValue11 = WorkerVariableService.Execute(runId, variables, evaluatedValue10).StatusCode;
        Assert.Equal(400, evaluatedValue11);
        WorkerVariableRequestDto evaluatedValue12 = new(WorkerProtocol.Version, runId, "list", Take: 201);
        var evaluatedValue13 = WorkerVariableService.Execute(runId, variables, evaluatedValue12).StatusCode;
        Assert.Equal(400, evaluatedValue13);
        WorkerVariableRequestDto evaluatedValue14 = new(WorkerProtocol.Version, runId, "list", Offset: -1);
        var evaluatedValue15 = WorkerVariableService.Execute(runId, variables, evaluatedValue14).StatusCode;
        Assert.Equal(400, evaluatedValue15);
        var stringValue = new string ('中', 16_385);
        var large = JsonSerializer.SerializeToElement(stringValue);
        WorkerVariableRequestDto evaluatedValue16 = new(WorkerProtocol.Version, runId, "set", "large", large, "string");
        var evaluatedValue17 = WorkerVariableService.Execute(runId, variables, evaluatedValue16).StatusCode;
        Assert.Equal(413, evaluatedValue17);
        var stringValue18 = new string ('a', 20_000);
        variables.Set("large", stringValue18);
        WorkerVariableRequestDto evaluatedValue19 = new(WorkerProtocol.Version, runId, "get", "large");
        var response = WorkerVariableService.Execute(runId, variables, evaluatedValue19);
        Assert.False(response.Variable!.ValueAvailable);
    }

    [Fact]
    public void JsonTextPreservesLargeIntegersAndDecimalPrecision()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        variables.Set("integer", 9_007_199_254_740_993L);
        variables.Set("decimal", 0.1234567890123456789012345678m);
        WorkerVariableRequestDto evaluatedValue20 = new(WorkerProtocol.Version, runId, "list");
        var result = WorkerVariableService.Execute(runId, variables, evaluatedValue20);
        var evaluatedValue21 = Assert.Single(result.Snapshot!.Variables, value => value.Name == "integer").ValueJson;
        Assert.Equal("9007199254740993", evaluatedValue21);
        var evaluatedValue22 = Assert.Single(result.Snapshot.Variables, value => value.Name == "decimal").ValueJson;
        Assert.Equal("0.1234567890123456789012345678", evaluatedValue22);
    }

    [Fact]
    public void TableSnapshotsCannotChangeTheTable()
    {
        var variables = new LibraryVariableService();
        variables.Set("count", 1);
        var snapshotResult = variables.Snapshot();
        var snapshot = Assert.IsType<Dictionary<string, object?>>(snapshotResult);
        snapshot["count"] = 9;
        variables.Set("new", 2);
        var getResult23 = variables.Get<int>("count");
        Assert.Equal(1, getResult23);
        var containsKeyResult = snapshot.ContainsKey("new");
        Assert.False(containsKeyResult);
    }

    [Fact]
    public void FinalCapturePreservesSafeValuesAndDoesNotEvaluateComplexObjects()
    {
        var runId = Guid.NewGuid();
        var variables = new LibraryVariableService();
        variables.Set("integer", 9_007_199_254_740_993L);
        var dangerousObject24 = new DangerousObject();
        variables.Set("handle", dangerousObject24);
        variables.Set("nonfinite", double.NaN);
        var snapshot = WorkerVariableService.CaptureFinal(runId, variables);
        Assert.True(snapshot.IsFinal);
        Assert.False(snapshot.IsTruncated);
        Assert.False(snapshot.HasMore);
        Assert.Equal(runId, snapshot.RunId);
        Assert.Equal(3, snapshot.TotalCount);
        var evaluatedValue25 = Assert.Single(snapshot.Variables, value => value.Name == "integer").ValueJson;
        Assert.Equal("9007199254740993", evaluatedValue25);
        var evaluatedValue26 = Assert.Single(snapshot.Variables, value => value.Name == "handle").UnavailableReason;
        Assert.Equal("unsupported_type", evaluatedValue26);
        var evaluatedValue27 = Assert.Single(snapshot.Variables, value => value.Name == "nonfinite").UnavailableReason;
        Assert.Equal("not_json", evaluatedValue27);
        variables.Set("integer", 0L);
        var evaluatedValue28 = Assert.Single(snapshot.Variables, value => value.Name == "integer").ValueJson;
        Assert.Equal("9007199254740993", evaluatedValue28);
    }

    [Fact]
    public void FinalCaptureFitsTheWorkerProtocolAndMarksOmittedData()
    {
        var variables = new LibraryVariableService();
        for (var i = 0; i < 2_100; i++)
        {
            var evaluatedValue29 = $"value-{i:D4}-{new string ('中', 150)}";
            var stringValue30 = new string ('a', 8_000);
            variables.Set(evaluatedValue29, stringValue30);
        }

        var stringValue31 = new string ('a', 257);
        variables.Set(stringValue31, 1);
        var newGuidResult32 = Guid.NewGuid();
        var snapshot = WorkerVariableService.CaptureFinal(newGuidResult32, variables);
        Assert.True(snapshot.IsTruncated);
        Assert.Equal(2_101, snapshot.TotalCount);
        Assert.InRange(snapshot.Variables.Count, 1, WorkerVariableService.MaxFinalVariables);
        Assert.All(snapshot.Variables, variable =>
        {
            Assert.InRange(variable.Name.Length, 1, 256);
        });
        Assert.Contains(snapshot.Variables, variable => variable.UnavailableReason == "snapshot_limit");
        var evaluatedValue33 = resolveGetByteCountResult() < WorkerVariableService.MaxFinalSnapshotBytes;
        Assert.True(evaluatedValue33);
        var result = new WorkerRunResultDto(WorkerProtocol.Version, snapshot.RunId, FlowRunStatusDto.Succeeded, null, null, snapshot);
        var serializePayloadResult34 = Worker.Protocol.WorkerProtocolCodec.SerializePayload(result);
        var message = Worker.Protocol.WorkerMessage.Create(Worker.Protocol.WorkerProtocolConstants.ResultKind, serializePayloadResult34, snapshot.RunId);
        var serializeResult = Worker.Protocol.WorkerProtocolCodec.Serialize(message);
        Assert.NotEmpty(serializeResult);
        int resolveGetByteCountResult()
        {
            var serializePayloadResult = Worker.Protocol.WorkerProtocolCodec.SerializePayload(snapshot);
            var getByteCountResult = System.Text.Encoding.UTF8.GetByteCount(serializePayloadResult);
            return getByteCountResult;
        }
    }

    private sealed class DangerousObject
    {
        public string Value
        {
            get
            {
                throw new InvalidOperationException("Getter must not be invoked.");
            }
        }

        public override string ToString()
        {
            throw new InvalidOperationException("ToString must not be invoked.");
        }
    }
}
