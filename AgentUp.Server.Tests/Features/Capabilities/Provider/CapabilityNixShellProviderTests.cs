using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityNixShellProviderTests
{
    [Test]
    public void ShellPath_writes_a_shell_pinned_to_the_first_party_nixpkgs_commit()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-shells");
        try
        {
            var path = new CapabilityNixShellProvider(root.FullName).ShellPath("opencode+dotnet", ["opencode", "dotnet-sdk_10"]);
            var expression = File.ReadAllText(path);

            Assert.That(path, Is.EqualTo(Path.Join(root.FullName, "capabilities", "nix", "opencode_dotnet", "default.nix")));
            Assert.That(expression, Does.Contain("pkgs.opencode pkgs.dotnet-sdk_10"));
            Assert.That(expression, Does.Contain("nixpkgs/archive/"));
        }
        finally { root.Delete(true); }
    }

    [Test]
    public void ShellPath_rewrites_a_shell_whose_packages_changed()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-shells-change");
        try
        {
            var provider = new CapabilityNixShellProvider(root.FullName);
            provider.ShellPath("dotnet", ["dotnet-sdk_9"]);

            var path = provider.ShellPath("dotnet", ["dotnet-sdk_10"]);

            Assert.That(File.ReadAllText(path), Does.Contain("dotnet-sdk_10").And.Not.Contain("dotnet-sdk_9"));
        }
        finally { root.Delete(true); }
    }
}
