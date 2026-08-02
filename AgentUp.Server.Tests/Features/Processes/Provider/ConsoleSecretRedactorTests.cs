using AgentUp.Server.Shared.Providers;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public sealed class ConsoleSecretRedactorTests
{
    [Test]
    public void Redact_masksCommonSecretAssignments()
    {
        var redactor = new ConsoleSecretRedactor();

        var result = redactor.Redact("token=abc api_key: key123 password = pass123");

        Assert.That(result, Is.EqualTo("token=[REDACTED] api_key: [REDACTED] password = [REDACTED]"));
    }

    [Test]
    public void Redact_masksUriCredentials()
    {
        var redactor = new ConsoleSecretRedactor();

        var result = redactor.Redact("postgres://user:pass@localhost/db");

        Assert.That(result, Is.EqualTo("postgres://[REDACTED]@localhost/db"));
    }
}
