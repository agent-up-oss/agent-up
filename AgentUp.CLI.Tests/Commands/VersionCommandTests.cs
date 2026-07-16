using AgentUp.CLI.Features.Workspaces.Controllers;

namespace AgentUp.CLI.Tests.Commands;

[TestFixture]
public class VersionCommandTests
{
    [Test]
    public async Task Version_ExitsZero_AndDoesNotRequireServer()
    {
        var output = new StringWriter();

        var exitCode = await new CliRunner("http://localhost:1", Directory.GetCurrentDirectory(), output).RunAsync(["--version"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString().Trim(), Is.Not.Empty);
    }

    [Test]
    public async Task VersionCommand_UsesSameOutputAsVersionOption()
    {
        var optionOutput = new StringWriter();
        var commandOutput = new StringWriter();

        await new CliRunner("http://localhost:1", Directory.GetCurrentDirectory(), optionOutput).RunAsync(["--version"]);
        await new CliRunner("http://localhost:1", Directory.GetCurrentDirectory(), commandOutput).RunAsync(["version"]);

        Assert.That(commandOutput.ToString(), Is.EqualTo(optionOutput.ToString()));
    }
}
