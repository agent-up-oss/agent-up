using AgentUp.AUDebug.Shared.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

[TestFixture]
public sealed class X11ToolRunnerTests
{
    [Test]
    public async Task Run_usesTheHostBinaryWhenItIsOnPath()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "ok\n", "") };
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        environment.Display = ":99";

        var result = await X11ToolRunner.RunAsync(
            processes,
            environment,
            "/tmp/au-debug-screens",
            "xdotool",
            "xdotool",
            ["search", "--class", "Agent-Up"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(processes.Ran.Single().FileName, Is.EqualTo("xdotool"));
            Assert.That(processes.Ran.Single().Environment!["DISPLAY"], Is.EqualTo(":99"));
        });
    }

    [Test]
    public async Task Run_wrapsTheToolInNixWhenItIsMissing()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "ok\n", "") };

        await X11ToolRunner.RunAsync(
            processes,
            new FakeEnvironment(),
            "/tmp/au-debug-screens",
            "import",
            "imagemagick",
            ["-window", "0x1", "shot.png"],
            CancellationToken.None,
            standardInput: "unused");

        Assert.Multiple(() =>
        {
            Assert.That(processes.Ran.Single().FileName, Is.EqualTo("nix-shell"));
            Assert.That(processes.Ran.Single().Arguments, Does.Contain("imagemagick"));
            Assert.That(processes.Ran.Single().Arguments, Does.Contain("import '-window' '0x1' 'shot.png'"));
            Assert.That(processes.Ran.Single().StandardInput, Is.EqualTo("unused"));
        });
    }
}
