using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Capabilities.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilitySoftwareProbeTests
{
    [Test]
    public void Software_nix_delivers_is_missing_only_as_nix()
    {
        var probe = Probe(nixAvailable: false, path: "");

        var missing = probe.Missing([new CapabilitySoftware("dotnet", true), new CapabilitySoftware("node", true)]);

        Assert.That(missing, Is.EqualTo(new[] { "nix" }));
    }

    [Test]
    public void An_executable_on_the_search_path_is_found()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-probe");
        try
        {
            var executable = Path.Join(directory.FullName, "codex-acp");
            File.WriteAllText(executable, "#!/bin/sh\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var missing = Probe(nixAvailable: true, path: directory.FullName)
                .Missing([new CapabilitySoftware("codex-acp", false), new CapabilitySoftware("claude-agent-acp", false)]);

            Assert.That(missing, Is.EqualTo(OperatingSystem.IsWindows() ? new[] { "codex-acp", "claude-agent-acp" } : new[] { "claude-agent-acp" }));
        }
        finally { directory.Delete(true); }
    }

    [Test]
    public void An_executable_in_the_dev_root_is_found_without_being_on_path()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-probe-dev");
        try
        {
            var bin = Directory.CreateDirectory(Path.Join(root.FullName, "bin")).FullName;
            var executable = Path.Join(bin, "agent");
            File.WriteAllText(executable, "#!/bin/sh\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            var probe = new CapabilitySoftwareProbe(
                new CapabilityModuleHarness.FixedNixPresence(true),
                new CapabilityRuntimePathProvider(root.FullName),
                () => "");

            Assert.That(probe.Missing([new CapabilitySoftware("agent", false)]), OperatingSystem.IsWindows() ? Is.Not.Empty : Is.Empty);
        }
        finally { root.Delete(true); }
    }

    private static CapabilitySoftwareProbe Probe(bool nixAvailable, string path)
        => new(new CapabilityModuleHarness.FixedNixPresence(nixAvailable), new CapabilityRuntimePathProvider(null), () => path);
}
