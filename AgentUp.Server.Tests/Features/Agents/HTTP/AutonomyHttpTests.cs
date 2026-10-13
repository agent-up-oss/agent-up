using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.Interfaces;
using AgentUp.Server.Features.Git.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.Agents.HTTP;

[TestFixture]
public sealed class AutonomyHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Test]
    public async Task GetAutonomy_returnsDisabledUntilAHostIsAuthorized()
    {
        await using var host = await HostAsync();

        var status = await host.Client.GetFromJsonAsync<ServerAutonomyDto>("/api/autonomy", Json);

        Assert.Multiple(() =>
        {
            Assert.That(status!.Enabled, Is.False);
            Assert.That(status.HostConnectionPresent, Is.False);
        });
    }

    [Test]
    public async Task PutAutonomy_refusesEnablementWithoutAHostConnection()
    {
        await using var host = await HostAsync();

        using var response = await host.Client.PutAsJsonAsync("/api/autonomy", new { enabled = true });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task PutHostConnectionThenEnable_makesTheServerAutonomous()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();

        using var authorized = await host.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/agent/host-connection",
            new { authorized = true });
        using var enabled = await host.Client.PutAsJsonAsync("/api/autonomy", new { enabled = true });
        var status = await host.Client.GetFromJsonAsync<ServerAutonomyDto>("/api/autonomy", Json);

        Assert.Multiple(() =>
        {
            Assert.That(authorized.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(enabled.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(status!.Enabled, Is.True);
            Assert.That(status.HostConnectionPresent, Is.True);
        });
    }

    [Test]
    public async Task PutBinding_recordsTheWorkspaceAgent()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/agent/binding",
            new { agent = "codex" });
        var session = await response.Content.ReadFromJsonAsync<AgentSessionDto>(Json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(session!.BoundAgent, Is.EqualTo("codex"));
        });
    }

    [Test]
    public async Task PutBinding_unknownWorkspaceReturnsNotFound()
    {
        await using var host = await HostAsync();

        using var response = await host.Client.PutAsJsonAsync(
            "/api/workspaces/missing/agent/binding",
            new { agent = "codex" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PutBinding_emptyAgentReturnsValidationProblem()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/agent/binding",
            new { agent = "" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PolicyRoundTrip_storesDeclaredRules()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();

        using var put = await host.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/agent/policy",
            new { rules = new[] { new { id = "write", kind = "WorktreeWrite", allow = true, scope = "src/" } } });
        var policy = await host.Client.GetFromJsonAsync<PermissionPolicyRequest>(
            $"/api/workspaces/{workspace.Id}/agent/policy", Json);

        Assert.Multiple(() =>
        {
            Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(policy!.Rules, Has.Count.EqualTo(1));
            Assert.That(policy.Rules[0].Id, Is.EqualTo("write"));
        });
    }

    [Test]
    public async Task RoleRoundTrip_storesAndClearsTheWorkspaceRole()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();

        using var put = await host.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/agent/role",
            new { prompt = "Ship it", tools = new[] { "read_file" }, trigger = "IssueAssigned", termination = "ProposalOpened" });
        using var get = await host.Client.GetAsync($"/api/workspaces/{workspace.Id}/agent/role");
        using var delete = await host.Client.DeleteAsync($"/api/workspaces/{workspace.Id}/agent/role");
        using var missing = await host.Client.GetAsync($"/api/workspaces/{workspace.Id}/agent/role");

        Assert.Multiple(() =>
        {
            Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });
    }

    [Test]
    public async Task PutAutonomyActivation_failsWhenTheServerIsDisabled()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/agent/autonomy",
            new { active = true });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task GetLoop_unknownWorkspaceReturnsNotFound()
    {
        await using var host = await HostAsync();

        using var response = await host.Client.GetAsync("/api/workspaces/missing/agent/loop");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetPolicy_unknownWorkspaceReturnsNotFound()
    {
        await using var host = await HostAsync();

        using var response = await host.Client.GetAsync("/api/workspaces/missing/agent/policy");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteBinding_clearsTheWorkspaceAgent()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();
        await host.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/agent/binding",
            new { agent = "codex" });

        using var response = await host.Client.DeleteAsync($"/api/workspaces/{workspace.Id}/agent/binding");
        var session = await response.Content.ReadFromJsonAsync<AgentSessionDto>(Json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(session!.BoundAgent, Is.Null);
        });
    }

    [Test]
    public async Task PutQueueOrder_storesTheDeclaredOrder()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/agent/queue-order",
            new { order = "UpdatedAsc" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task GetLoop_returnsStoppedForAKnownWorkspace()
    {
        await using var host = await HostAsync();
        var workspace = await host.RegisterAsync();

        var loop = await host.Client.GetFromJsonAsync<AgentLoopStatusDto>(
            $"/api/workspaces/{workspace.Id}/agent/loop", Json);

        Assert.Multiple(() =>
        {
            Assert.That(loop!.Active, Is.False);
            Assert.That(loop.Phase, Is.EqualTo("stopped"));
        });
    }

    [Test]
    public async Task PutAutonomy_disableReturnsNoContent()
    {
        await using var host = await HostAsync();

        using var response = await host.Client.PutAsJsonAsync("/api/autonomy", new { enabled = false });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    private static async Task<Host> HostAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = ["--urls=http://127.0.0.1:0"] });
        builder.Services.AddControllers().AddApplicationPart(typeof(AgentsHttpController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton(_ => new CapabilityReconciliationService());
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<WorkspaceEventBus>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<AgentCommandProvider>();
        builder.Services.AddSingleton<AgentSubscriptionAuth>();
        builder.Services.AddSingleton(_ => new AgentCliHomeProvider(Path.Join(Path.GetTempPath(), "agent-up-autonomy-http")));
        builder.Services.AddSingleton<IAgentClaudeCredentialStore, AgentClaudeCredentialStore>();
        builder.Services.AddSingleton<IAgentProcessEnvironmentProvider, AgentProcessEnvironmentProvider>();
        builder.Services.AddSingleton<AgentLoginCommandProvider>();
        builder.Services.AddSingleton<AgentLoginFlowProvider>();
        builder.Services.AddHttpClient("agent-login-callback");
        builder.Services.AddSingleton<AgentLoginCallbackRelay>();
        builder.Services.AddSingleton<IAgentSubscriptionLoginProvider, AgentSubscriptionLoginProvider>();
        builder.Services.AddSingleton<IAgentProcessFactory, AgentProcessFactory>();
        builder.Services.AddSingleton<AgentEventFrameProvider>();
        builder.Services.AddSingleton<AgentEventService>();
        builder.Services.AddSingleton(_ => new AgentSessionRepository(Path.Join(Path.GetTempPath(), $"agent-up-autonomy-sessions-{Guid.NewGuid():N}")));
        builder.Services.AddSingleton<AgentSchedulingService>();
        builder.Services.AddSingleton<AgentsController>();
        builder.Services.AddSingleton<IAgentWorkspaceSettingsRepository, InMemoryAgentWorkspaceSettingsRepository>();
        builder.Services.AddSingleton<IAutonomySettings, ConfiguredAutonomySettings>();
        builder.Services.AddSingleton<IAssignedIssueQueue, MemoryAssignedIssueQueue>();
        builder.Services.AddSingleton<IEnabledAgentInspector, EnabledAgentInspector>();
        builder.Services.AddSingleton<IRoleToolFilter, RoleToolFilterProvider>();
        builder.Services.AddSingleton<LoopInvariantGuard>();
        builder.Services.AddSingleton<AutonomousLoopService>();
        builder.Services.AddSingleton<IGitWorkingTreeProvider, FakeGitWorkingTreeProvider>();
        builder.Services.AddSingleton<IWorkspacePromptGuard, FakeWorkspacePromptGuard>();
        builder.Services.AddSingleton<GitChangeTreeService>();
        builder.Services.AddSingleton<GitQueryController>();
        builder.Services.AddSingleton<ICommitsQueueProvider, EmptyQueue>();
        builder.Services.AddSingleton<ICommitsGitProvider, EmptyGit>();
        builder.Services.AddSingleton<CommitPolicyProvider>();
        builder.Services.AddSingleton<CommitsService>();
        builder.Services.AddSingleton<CommitsController>();
        builder.Services.AddSingleton<LoopPreconditionProvider>();
        builder.Services.AddSingleton<AutonomousWorkspaceService>();
        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return new Host(app, new HttpClient { BaseAddress = new Uri(app.Urls.Single().TrimEnd('/') + "/") });
    }

    private sealed class Host(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public Task<AgentUp.Server.Features.Workspaces.DTOs.Workspace> RegisterAsync() =>
            app.Services.GetRequiredService<WorkspaceQueryController>().RegisterAsync(
                ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class EmptyQueue : AgentUp.Server.Features.Commits.Interfaces.ICommitsQueueProvider
    {
        public Task<AgentUp.Server.Features.Commits.Models.CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(AgentUp.Server.Features.Commits.Models.CommitsQueue.Empty());

        public Task WriteAsync(string worktreePath, AgentUp.Server.Features.Commits.Models.CommitsQueue queue, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class EmptyGit : AgentUp.Server.Features.Commits.Interfaces.ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<AgentUp.Server.Features.Commits.Models.GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(AgentUp.Server.Features.Commits.Models.GitOperationState.None);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
