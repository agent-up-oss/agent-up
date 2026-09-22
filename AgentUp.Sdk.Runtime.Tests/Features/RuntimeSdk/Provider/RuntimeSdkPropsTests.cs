using AgentUp.Sdk.Runtime;

namespace AgentUp.Sdk.Runtime.Tests.Features.RuntimeSdk.Provider;

[TestFixture]
public sealed class RuntimeSdkPropsTests
{
    [Test]
    public void Sdk_props_import_microsoft_net_sdk()
    {
        var props = File.ReadAllText(SdkFile("Sdk.props"));

        Assert.That(props, Does.Contain("Sdk=\"Microsoft.NET.Sdk\""));
        Assert.That(props, Does.Contain("Project=\"Sdk.props\""));
    }

    [Test]
    public void Sdk_targets_reference_the_runtime_contract_assembly()
    {
        var targets = File.ReadAllText(SdkFile("Sdk.targets"));

        Assert.That(targets, Does.Contain("AgentUp.Sdk.Runtime"));
        Assert.That(targets, Does.Contain("Project=\"Sdk.targets\""));
    }

    private static string SdkFile(string name)
    {
        var root = FindRepositoryRoot();
        return Path.Join(root, "AgentUp.Sdk.Runtime", "Sdk", name);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
