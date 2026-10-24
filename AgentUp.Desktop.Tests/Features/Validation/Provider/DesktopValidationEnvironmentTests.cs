using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.Providers;

namespace AgentUp.Desktop.Tests.Features.Validation.Provider;

public sealed class DesktopValidationEnvironmentTests
{
    [Test]
    public void ReadLaunch_isNullUnlessSurfaceAndOutAreSet()
    {
        using var unset = new Env(DesktopValidationKeys.Surface, null);
        using var missingOut = new Env(DesktopValidationKeys.Output, null);
        Environment.SetEnvironmentVariable(DesktopValidationKeys.Surface, "desktop");

        var none = new DesktopValidationEnvironment().ReadLaunch();
        Environment.SetEnvironmentVariable(DesktopValidationKeys.Output, "/tmp/out");
        var launch = new DesktopValidationEnvironment().ReadLaunch();

        Assert.That(none, Is.Null);
        Assert.That(launch, Is.Not.Null);
        Assert.That(launch!.OutputRoot, Is.EqualTo("/tmp/out"));
    }

    [Test]
    public void ReadLaunch_capturesFlowWorkspaceAndCosmeticSkip()
    {
        using var surface = new Env(DesktopValidationKeys.Surface, "desktop");
        using var output = new Env(DesktopValidationKeys.Output, "/tmp/out");
        using var flow = new Env(DesktopValidationKeys.Flow, "open-git");
        using var workspace = new Env(DesktopValidationKeys.Workspace, "ws-1");
        using var skip = new Env(DesktopValidationKeys.SkipCosmetic, "1");

        var launch = new DesktopValidationEnvironment().ReadLaunch();

        Assert.Multiple(() =>
        {
            Assert.That(launch, Is.Not.Null);
            Assert.That(launch!.FlowId, Is.EqualTo("open-git"));
            Assert.That(launch.WorkspaceId, Is.EqualTo("ws-1"));
            Assert.That(launch.SkipCosmeticStaging, Is.True);
            Assert.That(new DesktopValidationEnvironment().SkipCosmeticStaging, Is.True);
        });
    }

    private sealed class Env : IDisposable
    {
        private readonly string _key;
        private readonly string? _previous;

        public Env(string key, string? value)
        {
            _key = key;
            _previous = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_key, _previous);
    }
}
