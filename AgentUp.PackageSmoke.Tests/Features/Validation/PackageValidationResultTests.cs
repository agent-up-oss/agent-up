using AgentUp.PackageSmoke.Features.Validation;
using AgentUp.PackageSmoke.Features.Validation.DTOs;

namespace AgentUp.PackageSmoke.Tests.Features.Validation;

[TestFixture]
public class PackageValidationResultTests
{
    [Test]
    public void ToEnvironmentFile_quotesPathsForShell()
    {
        var result = new PackageValidationResult("/tmp/server path/AgentUp.Server", "/tmp/cli'path/AgentUp.CLI", []);

        var env = result.ToEnvironmentFile();

        Assert.That(env, Does.Contain("SERVER_PATH='/tmp/server path/AgentUp.Server'"));
        Assert.That(env, Does.Contain("CLI_PATH='/tmp/cli'\"'\"'path/AgentUp.CLI'"));
    }
}
