using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.AspNetCore;
using SereinFlow.Contracts;

namespace SereinFlow.Api.IntegrationTests;

public sealed class SecurityContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpHostRejectsMissingSecurityModeOrProjectScope(bool hostMode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSereinFlow(options =>
        {
            options.UseWebApi();
            if (hostMode)
            {
                options.UseHostSecurity();
            }
        });
        await using var app = builder.Build();
        Assert.Throws<InvalidOperationException>(() =>
        {
            app.UseSereinFlow();
        });
    }

    [Fact]
    public async Task HostPolicyReceivesActorResourceAndOperationAndExceptionsDoNotAllowAccess()
    {
        var principal = new ClaimsPrincipal();
        var actor = new SereinFlowActor("host:test", principal);
        var projectId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var resource = SereinFlowResource.Run(runId, projectId);
        var policy = new HostPolicy();
        var adapter = new HostAuthorization(policy);
        var rejected = await adapter.AuthorizeAsync(actor, SereinFlowOperations.RunRead, resource);
        Assert.False(rejected.IsAllowed);
        Assert.Same(principal, policy.Principal);
        Assert.Same(resource, policy.Resource);
        Assert.Equal(SereinFlowOperations.RunRead, policy.Operation);
        policy.Throw = true;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await adapter.AuthorizeAsync(actor, SereinFlowOperations.RunRead, resource);
        });
    }

    [Fact]
    public async Task ActorScopesRemainIsolatedAcrossConcurrentAsyncCalls()
    {
        var accessor = new SereinFlowActorAccessor();
        var principal = new ClaimsPrincipal();
        var parent = new SereinFlowActor("parent", principal);
        var first = new SereinFlowActor("first", principal);
        var second = new SereinFlowActor("second", principal);
        using var parentScope = accessor.Push(parent);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var call = Task.Run(async () =>
        {
            using (accessor.Push(first))
            {
                entered.SetResult();
                await released.Task;
                Assert.Same(first, accessor.Current);
            }
            Assert.Same(parent, accessor.Current);
        });
        await entered.Task;
        using (accessor.Push(second))
        {
            released.SetResult();
            await call;
            Assert.Same(second, accessor.Current);
        }
        Assert.Same(parent, accessor.Current);
    }

    private sealed class HostPolicy : IAuthorizationService
    {
        public ClaimsPrincipal? Principal { get; private set; }
        public object? Resource { get; private set; }
        public string? Operation { get; private set; }
        public bool Throw { get; set; }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        {
            Principal = user;
            Resource = resource;
            Operation = policyName;
            if (Throw)
            {
                throw new InvalidOperationException("Host policy unavailable.");
            }
            var denied = AuthorizationResult.Failed();
            var result = Task.FromResult(denied);
            return result;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) => throw new NotSupportedException();
    }
}
