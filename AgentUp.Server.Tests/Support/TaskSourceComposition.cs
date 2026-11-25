using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.Services;

namespace AgentUp.Server.Tests.Support;

/// <summary>A task source with no stored token, for tests that only need one to exist.</summary>
internal static class TaskSourceComposition
{
    public static TaskSourceController Controller(StubForgeCapability? forge = null)
    {
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges(forge ?? new StubForgeCapability());
        return new TaskSourceController(new TaskSourceService(
            new MemoryTaskSourceCredentialStore(),
            packages,
            new EmptyTaskSourceTeam()));
    }
}
