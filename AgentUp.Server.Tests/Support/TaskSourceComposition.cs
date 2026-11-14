using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.Providers;
using AgentUp.Server.Features.TaskSource.Services;

namespace AgentUp.Server.Tests.Support;

/// <summary>A task source backed by a directory of its own, for tests that only need one to exist.</summary>
internal static class TaskSourceComposition
{
    public static TaskSourceController Controller(StubForgeCapability? forge = null)
    {
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges(forge ?? new StubForgeCapability());
        return new TaskSourceController(new TaskSourceService(
            new MemoryTaskSourceStore(),
            new MemoryTaskSourceCredentialStore(),
            new TaskSourceStateStore(),
            packages,
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "https://example.test/callback")),
            new FixedForgeOAuthExchange("exchanged")));
    }
}
