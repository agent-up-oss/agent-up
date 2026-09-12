using AgentUp.Server.Composition;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class PackageSmokeEnvironmentTests
{
    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(PackageSmokeEnvironment.AuthDisabledVariable, null);
        Environment.SetEnvironmentVariable(PackageSmokeEnvironment.SentryDsnServerVariable, null);
    }

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

    [Test]
    public void ServerEnvironmentVariables_doesNotShipSentryDsnWhenPackagingVariableUnset()
    {
        Environment.SetEnvironmentVariable(PackageSmokeEnvironment.SentryDsnServerVariable, null);
        Environment.SetEnvironmentVariable(PackageSmokeEnvironment.AuthDisabledVariable, "true");

        Assert.That(
            PackageSmokeEnvironment.ServerEnvironmentVariables,
            Does.Not.ContainKey(PackageSmokeEnvironment.SentryDsnVariable));
    }

    [Test]
    public void ServerEnvironmentVariables_passesSentryDsnWhenPackagingVariableSet()
    {
        Environment.SetEnvironmentVariable(PackageSmokeEnvironment.SentryDsnServerVariable, "https://public@sentry.example/1");

        Assert.Multiple(() =>
        {
            Assert.That(
                PackageSmokeEnvironment.ServerEnvironmentVariables,
                Does.ContainKey(PackageSmokeEnvironment.SentryDsnVariable));
            Assert.That(
                PackageSmokeEnvironment.ServerEnvironmentVariables[PackageSmokeEnvironment.SentryDsnVariable],
                Is.EqualTo("https://public@sentry.example/1"));
            Assert.That(
                PackageSmokeEnvironment.ServerEnvironmentVariables,
                Does.Not.ContainKey("AGENTUP_AUTH_DISABLED"));
        });
    }
}
