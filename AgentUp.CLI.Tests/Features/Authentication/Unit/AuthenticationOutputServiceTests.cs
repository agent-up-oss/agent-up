using AgentUp.CLI.Features.Authentication.Models;
using AgentUp.CLI.Features.Authentication.Services;

namespace AgentUp.CLI.Tests.Features.Authentication.Unit;

[TestFixture]
public class AuthenticationOutputServiceTests
{
    [Test]
    public void WriteResult_returnsExitCodeAndWritesMessage()
    {
        using var output = new StringWriter();
        var exitCode = new AuthenticationOutputService(output)
            .WriteResult(AuthenticationCommandResult.Failure(AuthenticationMessages.LoginHint));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString().Trim(), Is.EqualTo(AuthenticationMessages.LoginHint));
        });
    }
}
