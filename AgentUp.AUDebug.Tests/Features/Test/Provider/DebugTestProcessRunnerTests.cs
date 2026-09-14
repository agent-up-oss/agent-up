using AgentUp.AUDebug.Features.Test.DTOs;
using AgentUp.AUDebug.Features.Test.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Test.Provider;

[TestFixture]
public sealed class DebugTestProcessRunnerTests
{
    [Test]
    public async Task Run_usesRepositoryRootForDotnetSuites()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "Passed!", "") };
        var paths = new FakePathValidator("/tmp/au-debug-tests");
        var runner = new DebugTestProcessRunner(processes, paths);

        await runner.RunAsync(
            new DebugTestStepDto("dotnet", ["test", "AgentUp.Desktop.Tests/AgentUp.Desktop.Tests.csproj", "--nologo"], "."),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Ran, Has.Count.EqualTo(1));
            Assert.That(processes.Ran[0].FileName, Is.EqualTo("dotnet"));
            Assert.That(processes.Ran[0].WorkingDirectory, Is.EqualTo(paths.RepositoryRoot));
            Assert.That(processes.Ran[0].Arguments, Does.Contain("--nologo"));
        });
    }

    [Test]
    public async Task Run_scopesNpmToThePackageDirectory()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "", "") };
        var paths = new FakePathValidator("/tmp/au-debug-tests");
        var runner = new DebugTestProcessRunner(processes, paths);

        await runner.RunAsync(new DebugTestStepDto("npm", ["test"], "AgentUp.DesignSystem"), CancellationToken.None);

        Assert.That(processes.Ran[0].WorkingDirectory, Is.EqualTo(Path.Join(paths.RepositoryRoot, "AgentUp.DesignSystem")));
    }
}
