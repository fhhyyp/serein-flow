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
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task SavedAndRestoredContentIsServedThroughTheSameMcpSession()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", ApiFactory.BootstrapKey);
        var resources = await client.GetFromJsonAsync<McpAiGuidanceResourceDto[]>(SkillsPath, JsonOptions);
        Assert.Equal(McpAiGuidance.AllResources.Count, resources!.Length);
        Assert.All(resources, resource => Assert.Equal(McpAiGuidanceSourceDto.Embedded, resource.Source));
        var original = await ReadSkill(client);

        using var initialize = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "initialize" });
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        client.DefaultRequestHeaders.Add("Mcp-Session-Id", Assert.Single(initialize.Headers.GetValues("Mcp-Session-Id")));
        using var save = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", new SaveMcpAiGuidanceRequestDto("# 从前端保存的技能\n使用当前 MCP 工具。", original.Revision));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.True(save.Headers.CacheControl!.NoStore);
        var saved = (await save.Content.ReadFromJsonAsync<McpAiGuidanceContentDto>(JsonOptions))!;
        Assert.Equal(McpAiGuidanceSourceDto.Disk, saved.Resource.Source);
        Assert.Equal(saved.Content, await ReadMcpSkill(client));

        using var staleSave = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", new SaveMcpAiGuidanceRequestDto("# stale", original.Revision));
        Assert.Equal(HttpStatusCode.Conflict, staleSave.StatusCode);
        using var staleReset = await client.PostAsJsonAsync($"{SkillsPath}/sereinlang/reset", new ResetMcpAiGuidanceRequestDto(original.Revision));
        Assert.Equal(HttpStatusCode.Conflict, staleReset.StatusCode);
        Assert.Equal(saved.Content, await ReadMcpSkill(client));

        using var reset = await client.PostAsJsonAsync($"{SkillsPath}/sereinlang/reset", new ResetMcpAiGuidanceRequestDto(saved.Revision));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(original, await ReadSkill(client));
        Assert.Equal(original.Content, await ReadMcpSkill(client));
        Assert.False(File.Exists(Path.Combine(factory.ContentRoot, "mcp/sereinlang-skill.md")));
    }

    [Fact]
    public async Task AdministratorIsRequiredEvenWhenOtherRestEndpointsAllowAnonymousAccess()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, SkillsPath), (HttpMethod.Get, $"{SkillsPath}/sereinlang"),
            (HttpMethod.Put, $"{SkillsPath}/sereinlang"), (HttpMethod.Post, $"{SkillsPath}/sereinlang/reset"),
        })
        {
            using var request = new HttpRequestMessage(method, path)
            {
                Content = method == HttpMethod.Get ? null : JsonContent.Create(new { content = "# attempt", expectedRevision = "stale" }),
            };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
        }

        using var scope = factory.Services.CreateScope();
        var project = SereinFlow.Domain.Project.Create("skill-authorization-test");
        await scope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var scopedKey = McpSecurityService.CreateKeyWithEntry(new CreateMcpApiKeyRequestDto(
            project.Id, "project-client", [McpPermissionDto.ProjectRead, McpPermissionDto.McpKeysManage], null));
        await scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>().AddAsync(scopedKey.Entry);
        client.DefaultRequestHeaders.Authorization = new("Bearer", scopedKey.Dto.Secret);
        using var denied = await client.GetAsync(SkillsPath);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var deniedSave = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", new SaveMcpAiGuidanceRequestDto("# denied", "stale"));
        Assert.Equal(HttpStatusCode.Forbidden, deniedSave.StatusCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(factory.ContentRoot));
    }

    [Fact]
    public async Task InvalidRequestsReturnStructuredErrorsWithoutCreatingOverrides()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", ApiFactory.BootstrapKey);
        var original = await ReadSkill(client);
        using var invalid = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", new SaveMcpAiGuidanceRequestDto(" ", original.Revision));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var error = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.Equal(McpErrorCodes.AiGuidanceContentInvalid, error.RootElement.GetProperty("code").GetString());
        using var oversized = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", new SaveMcpAiGuidanceRequestDto(new string('中', 180_000), original.Revision));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        using var missingRevision = await client.PutAsJsonAsync($"{SkillsPath}/sereinlang", new { content = "# missing revision" });
        Assert.Equal(HttpStatusCode.BadRequest, missingRevision.StatusCode);
        using var unknown = await client.PutAsJsonAsync($"{SkillsPath}/unregistered", new SaveMcpAiGuidanceRequestDto("# unknown", original.Revision));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(original, await ReadSkill(client));
        Assert.Empty(Directory.EnumerateFileSystemEntries(factory.ContentRoot));
    }

    private static async Task<McpAiGuidanceContentDto> ReadSkill(HttpClient client)
        => (await client.GetFromJsonAsync<McpAiGuidanceContentDto>($"{SkillsPath}/sereinlang", JsonOptions))!;

    private static async Task<string> ReadMcpSkill(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0", id = 2, method = "resources/read", @params = new { uri = McpAiGuidance.SereinLangResourceUri },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("result").GetProperty("contents")[0].GetProperty("text").GetString()!;
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const string BootstrapKey = "skill-management-integration-admin";
        private readonly DirectoryInfo _storage = Directory.CreateTempSubdirectory("sereinflow-skill-api-");
        public string ContentRoot => Path.Combine(_storage.FullName, "content");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(ContentRoot);
            builder.UseEnvironment("Testing");
            builder.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _storage.FullName,
                    ["SereinFlow:Mcp:BootstrapAdminKey"] = BootstrapKey,
                    ["SereinFlow:Api:RequireAuthentication"] = "false",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<McpAiGuidanceProvider>();
                services.AddSingleton(provider => new McpAiGuidanceProvider(provider.GetRequiredService<IConfiguration>(), ContentRoot));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;
            try
            {
                _storage.Delete(recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
