using AgentUp.Server.Composition;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class PackageSmokeEnvironmentTests
{
    [TearDown]
    public void TearDown() =>
        Environment.SetEnvironmentVariable(PackageSmokeEnvironment.AuthDisabledVariable, null);

    [Test]
    public void ServerEnvironmentVariables_injectsAuthDisabledWhenSmokeVariableSet()
    {
        Environment.SetEnvironmentVariable(PackageSmokeEnvironment.AuthDisabledVariable, "true");

        Assert.Multiple(() =>
        {
            Assert.That(
                PackageSmokeEnvironment.ServerEnvironmentVariables,
                Does.ContainKey("AGENTUP_AUTH_DISABLED"));
            Assert.That(
                PackageSmokeEnvironment.ServerEnvironmentVariables["AGENTUP_AUTH_DISABLED"],
                Is.EqualTo("true"));
        });
    }
}
