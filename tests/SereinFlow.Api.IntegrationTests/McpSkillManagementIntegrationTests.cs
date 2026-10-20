using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SereinFlow.Api;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Api.IntegrationTests;
public sealed class McpSkillManagementIntegrationTests
{
    private const string SkillsPath = "/api/environment/settings/mcp-skills";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        },
    };
    [Fact]
    public async Task SavedAndRestoredContentIsServedThroughTheSameMcpSession()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", ApiFactory.BootstrapKey);
        var resources = await client.GetFromJsonAsync<McpAiGuidanceResourceDto[]>(SkillsPath, JsonOptions);
        Assert.Equal(McpAiGuidance.AllResources.Count, resources!.Length);
        Assert.All(resources, resource =>
        {
            Assert.Equal(McpAiGuidanceSourceDto.Embedded, resource.Source);
        });
        var original = await ReadSkill(client);
        using var initialize = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "initialize" });
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        var getValuesResult = initialize.Headers.GetValues("Mcp-Session-Id");
        var singleResult = Assert.Single(getValuesResult);
        client.DefaultRequestHeaders.Add("Mcp-Session-Id", singleResult);
        var saveMcpAiGuidanceRequestDto = new SaveMcpAiGuidanceRequestDto("# 从前端保存的技能\n使用当前 MCP 工具。", original.Revision);
        using var save = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", saveMcpAiGuidanceRequestDto);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.True(save.Headers.CacheControl!.NoStore);
        var saved = (await save.Content.ReadFromJsonAsync<McpAiGuidanceContentDto>(JsonOptions))!;
        Assert.Equal(McpAiGuidanceSourceDto.Disk, saved.Resource.Source);
        var readMcpSkillResult = await ReadMcpSkill(client);
        Assert.Equal(saved.Content, readMcpSkillResult);
        var saveMcpAiGuidanceRequestDto1 = new SaveMcpAiGuidanceRequestDto("# stale", original.Revision);
        using var staleSave = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", saveMcpAiGuidanceRequestDto1);
        Assert.Equal(HttpStatusCode.Conflict, staleSave.StatusCode);
        var resetMcpAiGuidanceRequestDto = new ResetMcpAiGuidanceRequestDto(original.Revision);
        using var staleReset = await client.PostAsJsonAsync($"{SkillsPath}/sereinlang/reset", resetMcpAiGuidanceRequestDto);
        Assert.Equal(HttpStatusCode.Conflict, staleReset.StatusCode);
        var readMcpSkillResult2 = await ReadMcpSkill(client);
        Assert.Equal(saved.Content, readMcpSkillResult2);
        var resetMcpAiGuidanceRequestDto3 = new ResetMcpAiGuidanceRequestDto(saved.Revision);
        using var reset = await client.PostAsJsonAsync($"{SkillsPath}/sereinlang/reset", resetMcpAiGuidanceRequestDto3);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var readSkillResult = await ReadSkill(client);
        Assert.Equal(original, readSkillResult);
        var readMcpSkillResult4 = await ReadMcpSkill(client);
        Assert.Equal(original.Content, readMcpSkillResult4);
        var combineResult = Path.Combine(factory.ContentRoot, "mcp/sereinlang-skill.md");
        var existsResult = File.Exists(combineResult);
        Assert.False(existsResult);
    }

    [Fact]
    public async Task AdministratorIsRequiredEvenWhenOtherRestEndpointsAllowAnonymousAccess()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, SkillsPath),
            (HttpMethod.Get, $"{SkillsPath}/sereinlang"),
            (HttpMethod.Put, $"{SkillsPath}/sereinlang"),
            (HttpMethod.Post, $"{SkillsPath}/sereinlang/reset"),
        }

        )
        {
            using var request = new HttpRequestMessage(method, path)
            {
                Content = method == HttpMethod.Get ? null : JsonContent.Create(new { content = "# attempt", expectedRevision = "stale" })
            };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
        }

        using var scope = factory.Services.CreateScope();
        var project = SereinFlow.Domain.Project.Create("skill-authorization-test");
        var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult.AddAsync(project);
        var createMcpApiKeyRequestDto = new CreateMcpApiKeyRequestDto(project.Id, "project-client", [McpPermissionDto.ProjectRead, McpPermissionDto.McpKeysManage], null);
        var scopedKey = McpSecurityService.CreateKeyWithEntry(createMcpApiKeyRequestDto);
        var getRequiredServiceResult5 = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
        await getRequiredServiceResult5.AddAsync(scopedKey.Entry);
        client.DefaultRequestHeaders.Authorization = new("Bearer", scopedKey.Dto.Secret);
        using var denied = await client.GetAsync(SkillsPath);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var saveMcpAiGuidanceRequestDto6 = new SaveMcpAiGuidanceRequestDto("# denied", "stale");
        using var deniedSave = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", saveMcpAiGuidanceRequestDto6);
        Assert.Equal(HttpStatusCode.Forbidden, deniedSave.StatusCode);
        var enumerateFileSystemEntriesResult = Directory.EnumerateFileSystemEntries(factory.ContentRoot);
        Assert.Empty(enumerateFileSystemEntriesResult);
    }

    [Fact]
    public async Task InvalidRequestsReturnStructuredErrorsWithoutCreatingOverrides()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", ApiFactory.BootstrapKey);
        var original = await ReadSkill(client);
        var saveMcpAiGuidanceRequestDto7 = new SaveMcpAiGuidanceRequestDto(" ", original.Revision);
        using var invalid = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", saveMcpAiGuidanceRequestDto7);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var readAsStringAsyncResult = await invalid.Content.ReadAsStringAsync();
        using var error = JsonDocument.Parse(readAsStringAsyncResult);
        var getPropertyResult = error.RootElement.GetProperty("code");
        var getStringResult = getPropertyResult.GetString();
        Assert.Equal(McpErrorCodes.AiGuidanceContentInvalid, getStringResult);
        var stringValue = new string ('中', 180_000);
        var saveMcpAiGuidanceRequestDto8 = new SaveMcpAiGuidanceRequestDto(stringValue, original.Revision);
        using var oversized = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", saveMcpAiGuidanceRequestDto8);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        using var missingRevision = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", new { content = "# missing revision" });
        Assert.Equal(HttpStatusCode.BadRequest, missingRevision.StatusCode);
        var saveMcpAiGuidanceRequestDto9 = new SaveMcpAiGuidanceRequestDto("# unknown", original.Revision);
        using var unknown = await client.PutAsJsonAsync($"{SkillsPath}/unregistered", saveMcpAiGuidanceRequestDto9);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var readSkillResult10 = await ReadSkill(client);
        Assert.Equal(original, readSkillResult10);
        var enumerateFileSystemEntriesResult11 = Directory.EnumerateFileSystemEntries(factory.ContentRoot);
        Assert.Empty(enumerateFileSystemEntriesResult11);
    }

    private static async Task<McpAiGuidanceContentDto> ReadSkill(HttpClient client)
    {
        var evaluatedValue = (await client.GetFromJsonAsync<McpAiGuidanceContentDto>($"{SkillsPath}/sereinlang", JsonOptions))!;
        return evaluatedValue;
    }

    private static async Task<string> ReadMcpSkill(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 2, method = "resources/read", @params = new { uri = McpAiGuidance.SereinLangResourceUri }, });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var readAsStringAsyncResult12 = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(readAsStringAsyncResult12);
        var evaluatedValue18 = resolveGetStringResult()!;
        return evaluatedValue18;
        string? resolveGetStringResult()
        {
            var evaluatedValue15 = resolveGetPropertyResult()[0];
            var getPropertyResult16 = evaluatedValue15.GetProperty("text");
            var getStringResult17 = getPropertyResult16.GetString();
            return getStringResult17;
            JsonElement resolveGetPropertyResult()
            {
                var getPropertyResult13 = document.RootElement.GetProperty("result");
                var getPropertyResult14 = getPropertyResult13.GetProperty("contents");
                return getPropertyResult14;
            }
        }
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const string BootstrapKey = "skill-management-integration-admin";
        private readonly DirectoryInfo _storage = Directory.CreateTempSubdirectory("sereinflow-skill-api-");
        public string ContentRoot
        {
            get
            {
                var combineResult19 = Path.Combine(_storage.FullName, "content");
                return combineResult19;
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(ContentRoot);
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                var dictionary = new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _storage.FullName,
                    ["SereinFlow:Mcp:BootstrapAdminKey"] = BootstrapKey,
                    ["SereinFlow:Api:RequireAuthentication"] = "false",
                };
                configuration.AddInMemoryCollection(dictionary);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<McpAiGuidanceProvider>();
                services.AddSingleton(provider =>
                {
                    var getRequiredServiceResult20 = provider.GetRequiredService<IConfiguration>();
                    var mcpAiGuidanceProvider = new McpAiGuidanceProvider(getRequiredServiceResult20, ContentRoot);
                    return mcpAiGuidanceProvider;
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                return;
            }

            try
            {
                _storage.Delete(recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
