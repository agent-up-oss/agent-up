using AgentUp.Sdk.Forge;

namespace AgentUp.Sdk.Forge.Tests.Features.ForgeSdk.Provider;

[TestFixture]
public sealed class ForgeSdkPropsTests
{
    [Test]
    public void Sdk_props_import_microsoft_net_sdk()
    {
        var props = File.ReadAllText(SdkFile("Sdk.props"));

        Assert.That(props, Does.Contain("Sdk=\"Microsoft.NET.Sdk\""));
        Assert.That(props, Does.Contain("net10.0"));
    }

    [Test]
    public void Sdk_targets_reference_the_forge_contract_assembly()
    {
        var targets = File.ReadAllText(SdkFile("Sdk.targets"));

        Assert.That(targets, Does.Contain("AgentUp.Sdk.Forge"));
        Assert.That(targets, Does.Contain("AgentUp.Sdk.Common"));
    }

    private static string SdkFile(string name)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.sln")))
                return Path.Join(directory.FullName, "AgentUp.Sdk.Forge", "Sdk", name);
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
