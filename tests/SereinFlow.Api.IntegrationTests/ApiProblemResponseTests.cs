using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.Api.Controllers;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Api.IntegrationTests;
public sealed class ApiProblemResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();
    [Fact]
    public void ExplicitNullCodeIsRetainedAtTheTopLevel()
    {
        var controller = new ProblemController();
        var response = controller.WithCode(null);
        var json = ReadProblem(response);
        var code = json.GetProperty("code");
        Assert.Equal(JsonValueKind.Null, code.ValueKind);
        var hasVersion = json.TryGetProperty("currentVersion", out _);
        Assert.False(hasVersion);
        var hasNestedExtensions = json.TryGetProperty("extensions", out _);
        Assert.False(hasNestedExtensions);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("application/problem+json", response.ContentTypes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(42L)]
    public void ExplicitVersionRetainsNullAndZero(long? version)
    {
        var controller = new ProblemController();
        var response = controller.WithVersion(version);
        var json = ReadProblem(response);
        var value = json.GetProperty("currentVersion");
        if (version is null)
        {
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }
        else
        {
            var actual = value.GetInt64();
            Assert.Equal(version.Value, actual);
        }

        var hasCode = json.TryGetProperty("code", out _);
        Assert.False(hasCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(42L)]
    public void SubmissionOmitsAnUnavailableVersion(long? version)
    {
        var controller = new ProblemController();
        var submission = new RunSubmissionResult(null, 409, "Conflict", CurrentVersion: version);
        var result = controller.Submission(submission);
        var response = Assert.IsType<ObjectResult>(result);
        var json = ReadProblem(response);
        var hasVersion = json.TryGetProperty("currentVersion", out _);
        Assert.Equal(version.HasValue, hasVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AuthorizationOmitsAnEmptyCode(string? code)
    {
        var controller = new ProblemController();
        var authorization = new SereinFlowApiAuthorizationResult(false, 403, code);
        var result = controller.Authorization(authorization);
        var response = Assert.IsType<ObjectResult>(result);
        var json = ReadProblem(response);
        var hasCode = json.TryGetProperty("code", out _);
        Assert.False(hasCode);
        var title = json.GetProperty("title").GetString();
        Assert.Equal("The request could not be completed. 请求无法完成。", title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void UpgradeRetainsCodeAndOnlyIncludesAvailableVersion(long? version)
    {
        var controller = new ProblemController();
        var upgrade = new LibraryUpgradeOperationResult<string>(false, 409, Code: null, CurrentVersion: version);
        var result = controller.Upgrade(upgrade);
        var response = Assert.IsType<ObjectResult>(result);
        var json = ReadProblem(response);
        var code = json.GetProperty("code");
        Assert.Equal(JsonValueKind.Null, code.ValueKind);
        var hasVersion = json.TryGetProperty("currentVersion", out _);
        Assert.Equal(version.HasValue, hasVersion);
    }

    [Fact]
    public void ArbitraryExtensionsRemainSupported()
    {
        var controller = new ProblemController();
        using var document = JsonDocument.Parse("{\"path\":\"nodes[0]\",\"retryable\":false}");
        var extensions = document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property =>
        {
            var value = (object? )property.Value.Clone();
            return value;
        }, StringComparer.Ordinal);
        var response = controller.WithExtensions(extensions);
        var json = ReadProblem(response);
        var path = json.GetProperty("path").GetString();
        var retryable = json.GetProperty("retryable").GetBoolean();
        Assert.Equal("nodes[0]", path);
        Assert.False(retryable);
    }

    private static JsonElement ReadProblem(ObjectResult response)
    {
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        var json = JsonSerializer.SerializeToElement(problem, JsonOptions);
        return json;
    }

    private sealed class ProblemController : ApiControllerBase
    {
        public ObjectResult WithCode(string? code)
        {
            var result = ApiProblemWithCode(409, "Conflict", code);
            return result;
        }

        public ObjectResult WithVersion(long? version)
        {
            var result = ApiVersionProblem(409, "Conflict", version);
            return result;
        }

        public ActionResult Submission(RunSubmissionResult submission)
        {
            var result = ToRunSubmissionResponse(submission);
            return result;
        }

        public ActionResult? Authorization(SereinFlowApiAuthorizationResult authorization)
        {
            var result = ToAuthorizationFailure(authorization);
            return result;
        }

        public ActionResult Upgrade(LibraryUpgradeOperationResult<string> upgrade)
        {
            var result = ToLibraryUpgradeResponse(upgrade);
            return result;
        }

        public ObjectResult WithExtensions(IReadOnlyDictionary<string, object?> extensions)
        {
            var result = ApiProblem(400, "Invalid request", extensions: extensions);
            return result;
        }
    }
}
