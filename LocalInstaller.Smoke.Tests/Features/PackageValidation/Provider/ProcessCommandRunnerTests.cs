using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Features.PackageValidation.Providers;

namespace LocalInstaller.Smoke.Tests.Features.PackageValidation.Provider;

[TestFixture]
public class ProcessCommandRunnerTests
{
    [Test]
    public async Task RunAsync_rejectsUnknownCommandNames()
    {
        var result = await new ProcessCommandRunner().RunAsync(new CommandSpec("agent-up-command-that-does-not-exist", []));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("not allowed"));
    }

    [Test]
    public async Task RunAsync_rejectsRelativeExecutablePaths()
    {
        var result = await new ProcessCommandRunner().RunAsync(new CommandSpec("tools/agent-up", []));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("paths are not allowed"));
    }

    [Test]
    public async Task RunAsync_allowsWindowsInstallCorePowerShellCommandShape()
    {
        var result = await new ProcessCommandRunner().RunAsync(
            new CommandSpec("powershell.exe", [
                "-NoProfile",
                "-Command",
                "& $env:LOCALINSTALLER_SMOKE_INSTALLER_APP --payload-root $env:LOCALINSTALLER_SMOKE_PAYLOAD_ROOT --install-core; $exit = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }; if ($exit -ne 0) { $log = Join-Path $env:LOCALAPPDATA 'LocalInstaller\\Logs\\installer.log'; if (Test-Path $log) { Get-Content -Tail 120 $log | Write-Error } }; exit $exit"
            ], Environment: new Dictionary<string, string>
            {
                ["LOCALINSTALLER_SMOKE_INSTALLER_APP"] = @"C:\Program Files\Agent-Up\installer\LocalInstaller.App.exe",
                ["LOCALINSTALLER_SMOKE_PAYLOAD_ROOT"] = @"C:\Program Files\Agent-Up\installer\payload"
            }));

        Assert.That(result.ExitCode, Is.Not.EqualTo(126), result.Stderr);
        Assert.That(result.Stderr, Does.Not.Contain("PowerShell arguments are not allowed"));
        Assert.That(result.Stderr, Does.Not.Contain("Command executable paths are not allowed"));
    }

    [Test]
    public async Task RunAsync_rejectsUnsafeEnvironmentKeys()
    {
        var result = await new ProcessCommandRunner().RunAsync(
            new CommandSpec("git", [], Environment: new Dictionary<string, string>
            {
                ["BAD-KEY"] = "value"
            }));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("Environment variable name"));
    }

    [Test]
    public async Task RunAsync_rejectsNonAbsoluteWorkingDirectory()
    {
        var result = await new ProcessCommandRunner().RunAsync(
            new CommandSpec("git", [], "relative-workdir"));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("working directory"));
    }

    [Test]
    public async Task RunAsync_allowsDotnetSmokeRestoreAndBuildInWorkingDirectory()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-ProcessCommandRunner", Guid.NewGuid().ToString());
        var projectDir = Path.Join(workDir, "SmokeDotnet");

        try
        {
            Directory.CreateDirectory(projectDir);
            await File.WriteAllTextAsync(Path.Join(projectDir, "SmokeDotnet.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var runner = new ProcessCommandRunner();
            var project = Path.Join(projectDir, "SmokeDotnet.csproj");

            var restore = await runner.RunAsync(new CommandSpec("dotnet", ["restore", project]));
            var build = await runner.RunAsync(new CommandSpec("dotnet", ["build", project, "--no-restore"]));

            Assert.That(restore.ExitCode, Is.EqualTo(0), restore.Stderr + restore.Stdout);
            Assert.That(build.ExitCode, Is.EqualTo(0), build.Stderr + build.Stdout);
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    [Test]
    public async Task RunAsync_rejectsOtherDotnetArguments()
    {
        var result = await new ProcessCommandRunner().RunAsync(new CommandSpec("dotnet", ["--info"]));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("dotnet arguments are not allowed"));
    }
}
