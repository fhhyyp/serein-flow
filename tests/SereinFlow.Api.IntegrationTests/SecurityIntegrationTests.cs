using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.AspNetCore;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Security.ApiKeys;

namespace SereinFlow.Api.IntegrationTests;
public sealed class SecurityIntegrationTests
{
    [Fact]
    public async Task CookieSessionDoesNotContainSecretAndRevocationDeniesFurtherRequests()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        {
            var actual1 = (await client.GetAsync("/api/projects")).StatusCode;
            Assert.Equal(HttpStatusCode.Unauthorized, actual1);
        }

        var capabilities = await client.GetFromJsonAsync<Capabilities>("/api/security/capabilities");
        Assert.True(capabilities!.ApiKeyManagement);
        Assert.True(capabilities.InitialSetupRequired);
        var setup = await client.PostAsync("/api/security/api-keys/initial-admin", null);
        Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        var created = (await setup.Content.ReadFromJsonAsync<CreatedApiKeyDto>())!;
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/security/session");
        login.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.Secret);
        var loggedIn = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.NoContent, loggedIn.StatusCode);
        var argument1 = loggedIn.Headers.GetValues("Set-Cookie");
        var cookie = Assert.Single(argument1);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(created.Secret, cookie);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        {
            var actual2 = (await client.GetAsync("/api/projects")).StatusCode;
            Assert.Equal(HttpStatusCode.OK, actual2);
        }

        {
            var actual3 = (await client.GetAsync("/api/security/session")).StatusCode;
            Assert.Equal(HttpStatusCode.OK, actual3);
        }

        var revoked = await client.DeleteAsync("/api/security/api-keys/" + created.Key.Id);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        {
            var actual4 = (await client.GetAsync("/api/projects")).StatusCode;
            Assert.Equal(HttpStatusCode.Unauthorized, actual4);
        }

        {
            var actual5 = (await client.GetAsync("/api/security/session")).StatusCode;
            Assert.Equal(HttpStatusCode.Unauthorized, actual5);
        }

        {
            var actual6 = (await client.GetAsync("/api/environment/settings/mcp-keys")).StatusCode;
            Assert.Equal(HttpStatusCode.NotFound, actual6);
        }
    }

    [Fact]
    public async Task InitializationRejectsForwardedRequestsAndConcurrentSetupCreatesOneKey()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        using var proxied = new HttpRequestMessage(HttpMethod.Post, "/api/security/api-keys/initial-admin");
        proxied.Headers.Add("X-Forwarded-For", "203.0.113.1");
        {
            var actual7 = (await client.SendAsync(proxied)).StatusCode;
            Assert.Equal(HttpStatusCode.Forbidden, actual7);
        }

        var argument2 = Enumerable.Range(0, 4).Select(_ =>
        {
            var value1 = client.PostAsync("/api/security/api-keys/initial-admin", null);
            return value1;
        });
        var results = await Task.WhenAll(argument2);
        {
            var actual8 = results.Where(result => result.StatusCode == HttpStatusCode.Created);
            Assert.Single(actual8);
        }

        {
            var actual9 = results.Count(result => result.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(3, actual9);
        }

        foreach (var result in results)
            result.Dispose();
    }

    [Fact]
    public async Task ExistingHostIdentityAndMultipleProjectScopeWorkWithoutSereinFlowKey()
    {
        using var factory = new Factory(host: true);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var first = Project.Create("first");
        var second = Project.Create("second");
        var hidden = Project.Create("hidden");
        await repository.AddAsync(first);
        await repository.AddAsync(second);
        await repository.AddAsync(hidden);
        factory.Projects.UnionWith([first.Id, second.Id]);
        var readModels = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        var pageOptions = new AiReadModelOptions(MaxItems: 1);
        var visibleScope = new ProjectAccessScope(false, factory.Projects);
        var page = await readModels.ListProjectsAsync(pageOptions, accessScope: visibleScope);
        var pageItem = Assert.Single(page.Items);
        Assert.Contains(pageItem.Id, factory.Projects);
        Assert.True(page.HasMore);
        var singleProject = new HashSet<Guid> { second.Id };
        var singleScope = new ProjectAccessScope(false, singleProject);
        var singlePage = await readModels.ListProjectsAsync(pageOptions, accessScope: singleScope);
        var onlyItem = Assert.Single(singlePage.Items);
        Assert.Equal(second.Id, onlyItem.Id);
        Assert.False(singlePage.HasMore);
        client.DefaultRequestHeaders.Add("Test-Host-Identity", "accepted");
        var projects = await client.GetFromJsonAsync<ProjectWorkspaceDto[]>("/api/projects");
        {
            var actual10 = new[]
            {
                first.Id,
                second.Id
            }.Order();
            var actual11 = projects!.Select(project => project.Project.Id).Order();
            Assert.Equal(actual10, actual11);
        }

        var capabilities = await client.GetFromJsonAsync<Capabilities>("/api/security/capabilities");
        Assert.Equal("Host", capabilities!.Mode);
        Assert.False(capabilities.ApiKeyManagement);
        var options = factory.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal("ExistingHost", options.DefaultScheme);
        client.DefaultRequestHeaders.Remove("Test-Host-Identity");
        {
            var actual12 = (await client.GetAsync("/api/projects")).StatusCode;
            Assert.Equal(HttpStatusCode.Unauthorized, actual12);
        }

        client.DefaultRequestHeaders.Add("Test-Host-Identity", "denied");
        {
            var actual13 = (await client.GetAsync("/api/projects")).StatusCode;
            Assert.Equal(HttpStatusCode.Forbidden, actual13);
        }
    }

    [Fact]
    public void ActorScopesRestorePreviousIdentity()
    {
        var accessor = new SereinFlowActorAccessor();
        var argument3 = new ClaimsPrincipal();
        var first = new SereinFlowActor("first", argument3);
        var argument4 = new ClaimsPrincipal();
        var second = new SereinFlowActor("second", argument4);
        using (accessor.Push(first))
        {
            using (accessor.Push(second))
                Assert.Same(second, accessor.Current);
            Assert.Same(first, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task HubSubscriptionsEnforceProjectScopeAndRecheckRevokedKeys()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        {
            var actual14 = (await client.PostAsync("/hubs/workspace/negotiate?negotiateVersion=1", null)).StatusCode;
            Assert.Equal(HttpStatusCode.Unauthorized, actual14);
        }

        using var scope = factory.Services.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = Project.Create("visible");
        var hidden = Project.Create("hidden");
        await projects.AddAsync(project);
        await projects.AddAsync(hidden);
        var argument5 = new CreateApiKeyRequest(project.Id, "hub-key", [SereinFlowPermission.ProjectRead]);
        var key = ApiKeyService.CreateKeyWithEntry(argument5);
        var store = scope.ServiceProvider.GetRequiredService<IApiKeyStore>();
        await store.AddAsync(key.Entry);
        var socketClient = factory.Server.CreateWebSocketClient();
        socketClient.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + key.Dto.Secret;
        var argument6 = new Uri("ws://localhost/hubs/workspace");
        using var socket = await socketClient.ConnectAsync(argument6, CancellationToken.None);
        await SendAsync(socket, new { protocol = "json", version = 1 });
        {
            var actual15 = await ReceiveAsync(socket);
            Assert.Equal("{}\u001e", actual15);
        }

        await SendAsync(socket, new { type = 1, invocationId = "1", target = "SubscribeProject", arguments = new[] { hidden.Id } });
        {
            var actual16 = await ReceiveAsync(socket);
            Assert.Contains("\"error\"", actual16);
        }

        await SendAsync(socket, new { type = 1, invocationId = "2", target = "SubscribeProject", arguments = new[] { project.Id } });
        {
            var actual17 = await ReceiveAsync(socket);
            Assert.DoesNotContain("\"error\"", actual17);
        }

        await store.UpdateAsync(key.Entry with { RevokedAt = DateTimeOffset.UtcNow });
        await SendAsync(socket, new { type = 1, invocationId = "3", target = "SubscribeProject", arguments = new[] { project.Id } });
        {
            var actual18 = await ReceiveAsync(socket);
            Assert.Contains("\"error\"", actual18);
        }

        socket.Abort();
    }

    [Fact]
    public async Task LegacyStoredPermissionFormatsAndExpiredKeysRemainCorrect()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var project = Project.Create("legacy");
        await scope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var argument7 = new CreateApiKeyRequest(project.Id, "legacy-key", [SereinFlowPermission.ProjectRead]);
        var key = ApiKeyService.CreateKeyWithEntry(argument7);
        var store = scope.ServiceProvider.GetRequiredService<IApiKeyStore>();
        await store.AddAsync(key.Entry);
        var sql = scope.ServiceProvider.GetRequiredService<SqlSugar.ISqlSugarClient>();
        await sql.Updateable<ApiKeyRecord>().SetColumns(record => record.PermissionsJson == "[0,\"libraryRead\",\"mcp.keys.manage\"]").Where(record => record.Id == key.Entry.Id).ExecuteCommandAsync();
        var loaded = await store.FindAsync(key.Entry.Id);
        Assert.Contains(SereinFlowPermission.ProjectRead, loaded!.Permissions);
        Assert.Contains(SereinFlowPermission.LibraryRead, loaded.Permissions);
        Assert.Contains(SereinFlowPermission.SecurityManage, loaded.Permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key.Dto.Secret);
        {
            var actual19 = (await client.GetAsync("/api/projects")).StatusCode;
            Assert.Equal(HttpStatusCode.OK, actual19);
        }

        var expiredAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await store.UpdateAsync(loaded with { ExpiresAt = expiredAt });
        {
            var actual20 = (await client.GetAsync("/api/projects")).StatusCode;
            Assert.Equal(HttpStatusCode.Unauthorized, actual20);
        }
    }

    private static Task SendAsync(WebSocket socket, object value)
    {
        var json = JsonSerializer.Serialize(value) + "\u001e";
        var bytes = Encoding.UTF8.GetBytes(json);
        var message = new ArraySegment<byte>(bytes);
        var task = socket.SendAsync(message, WebSocketMessageType.Text, true, CancellationToken.None);
        return task;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SseUsesCurrentSessionAndAuthorizesTheStoredRunResource(bool hostMode)
    {
        using var factory = new Factory(hostMode);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var project = Project.Create("sse-visible");
        var hidden = Project.Create("sse-hidden");
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await projects.AddAsync(project);
        await projects.AddAsync(hidden);
        factory.Projects.Add(project.Id);
        var runs = scope.ServiceProvider.GetRequiredService<IFlowRunStore>();
        var flowId = Guid.NewGuid();
        var definition = new FlowDefinitionDto(flowId, 1, 1, [], "", "");
        var checksum = FlowDiffService.GetChecksum(definition);
        definition = definition with { Checksum = checksum };
        var hiddenFlowId = Guid.NewGuid();
        var hiddenDefinition = definition with { Id = hiddenFlowId };
        var hiddenChecksum = FlowDiffService.GetChecksum(hiddenDefinition);
        hiddenDefinition = hiddenDefinition with { Checksum = hiddenChecksum };
        var flows = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await flows.AddAsync(project.Id, definition);
        await flows.AddAsync(hidden.Id, hiddenDefinition);
        var now = DateTimeOffset.UtcNow;
        var visibleRun = FlowRun.Start(project.Id, flowId, 1, now);
        var hiddenRun = FlowRun.Start(hidden.Id, hiddenFlowId, 1, now);
        visibleRun.Complete(FlowRunStatus.Succeeded, now);
        hiddenRun.Complete(FlowRunStatus.Succeeded, now);
        await runs.CreateWithSnapshotAsync(visibleRun, definition);
        await runs.CreateWithSnapshotAsync(hiddenRun, hiddenDefinition);
        var route = $"/api/runs/{visibleRun.Id:D}/events/stream";
        using var anonymous = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        if (hostMode)
        {
            client.DefaultRequestHeaders.Add("Test-Host-Identity", "accepted");
        }
        else
        {
            var request = new CreateApiKeyRequest(project.Id, "sse-key", [SereinFlowPermission.RunRead]);
            var key = ApiKeyService.CreateKeyWithEntry(request);
            var store = scope.ServiceProvider.GetRequiredService<IApiKeyStore>();
            await store.AddAsync(key.Entry);
            using var login = new HttpRequestMessage(HttpMethod.Post, "/api/security/session");
            login.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Dto.Secret);
            using var loggedIn = await client.SendAsync(login);
            Assert.Equal(HttpStatusCode.NoContent, loggedIn.StatusCode);
        }
        using var allowed = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal("text/event-stream", allowed.Content.Headers.ContentType?.MediaType);
        var hiddenRoute = $"/api/runs/{hiddenRun.Id:D}/events/stream";
        using var denied = await client.GetAsync(hiddenRoute);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
    private static async Task<string> ReceiveAsync(WebSocket socket)
    {
        var argument8 = TimeSpan.FromSeconds(10);
        using var timeout = new CancellationTokenSource(argument8);
        var buffer = new byte[4096];
        var argument9 = new ArraySegment<byte>(buffer);
        var response = await socket.ReceiveAsync(argument9, timeout.Token);
        {
            var result21 = Encoding.UTF8.GetString(buffer, 0, response.Count);
            return result21;
        }
    }

    private sealed record Capabilities(string Mode, bool ApiKeyManagement, bool InitialSetupRequired);
    private sealed class Factory(bool host = false) : WebApplicationFactory<Program>
    {
        private readonly string _root = CreateDataRoot();

        private static string CreateDataRoot()
        {
            var temp = Path.GetTempPath();
            var id = Guid.NewGuid().ToString("N");
            var path = Path.Combine(temp, "sereinflow-security-" + id);
            return path;
        }
        public HashSet<Guid> Projects { get; } = [];

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                var argument10 = new Dictionary<string, string?>
                {
                    ["SereinFlow:DataRoot"] = _root,
                    ["Logging:EventLog:LogLevel:Default"] = "None"
                };
                configuration.AddInMemoryCollection(argument10);
            });
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddSingleton<IStartupFilter, LoopbackTestStartupFilter>();
                if (!host)
                {
                    return;
                }

                services.RemoveAll<SereinFlowHttpSecurityOptions>();
                var argument11 = new SereinFlowHttpSecurityOptions
                {
                    Mode = "Host"
                };
                services.AddSingleton(argument11);
                services.RemoveAll<ISereinFlowAuthorizationService>();
                services.AddScoped<ISereinFlowAuthorizationService, HostAuthorization>();
                services.RemoveAll<ISereinFlowProjectScopeProvider>();
                var argument12 = new TestScopes(Projects);
                services.AddSingleton<ISereinFlowProjectScopeProvider>(argument12);
                services.AddAuthentication("ExistingHost").AddScheme<AuthenticationSchemeOptions, HostHandler>("ExistingHost", _ =>
                {
                });
                services.AddAuthorization(options =>
                {
                    options.AddPolicy(SereinFlowOperations.ProjectRead, policy =>
                    {
                        policy.RequireClaim("grant", SereinFlowOperations.ProjectRead);
                    });
                    options.AddPolicy(SereinFlowOperations.RunRead, policy =>
                    {
                        policy.RequireAssertion(CanReadRun);
                    });
                });
                services.AddSingleton<IStartupFilter, HostAuthenticationStartupFilter>();
            });
        }

        private bool CanReadRun(AuthorizationHandlerContext context)
        {
            var allowed = context.User.HasClaim("grant", SereinFlowOperations.ProjectRead)
                && context.Resource is SereinFlowResource { Kind: "run", ProjectId: { } project }
                && Projects.Contains(project);
            return allowed;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_root))
            {
                try
                {
                    Directory.Delete(_root, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private sealed class TestScopes(HashSet<Guid> projects) : ISereinFlowProjectScopeProvider
    {
        public ValueTask<ProjectAccessScope> GetScopeAsync(SereinFlowActor actor, string operation, CancellationToken cancellationToken = default)
        {
            var scope = new ProjectAccessScope(false, projects);
            var result = ValueTask.FromResult(scope);
            return result;
        }
    }

    private sealed class HostHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = Request.Headers["Test-Host-Identity"].ToString();
            if (identity.Length == 0)
            {
                var argument13 = AuthenticateResult.NoResult();
                var result22 = Task.FromResult(argument13);
                return result22;
            }

            var claims = new List<Claim>
            {
                new("sub", "external-user", ClaimValueTypes.String, "external-issuer")
            };
            if (identity == "accepted")
            {
                var grant = new Claim("grant", SereinFlowOperations.ProjectRead);
                claims.Add(grant);
            }

            var argument16 = new ClaimsIdentity(claims, "ExistingHost");
            var principal = new ClaimsPrincipal(argument16);
            {
                var argument15 = new AuthenticationTicket(principal, "ExistingHost");
                var argument14 = AuthenticateResult.Success(argument15);
                var result23 = Task.FromResult(argument14);
                return result23;
            }
        }
    }

    private sealed class HostAuthenticationStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            Action<IApplicationBuilder> configure = app =>
            {
                app.UseAuthentication();
                next(app);
            };
            return configure;
        }
    }
}
