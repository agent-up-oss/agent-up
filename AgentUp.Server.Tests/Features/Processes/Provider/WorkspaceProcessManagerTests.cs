using System.ComponentModel;
using System.Diagnostics;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Processes.Providers;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public class WorkspaceProcessManagerTests
{
    private InMemoryOutputRepository _output = null!;
    private WorkspaceRegistry _registry = null!;
    private WorkspaceProcessManager _manager = null!;

    [SetUp]
    public async Task SetUp()
    {
        _output = new InMemoryOutputRepository();
        _registry = ServerTestComposition.CreateRegistry();
        await ((IHostedService)_registry).StartAsync(CancellationToken.None);
        _manager = new WorkspaceProcessManager(
            ServerTestComposition.CreateWorkspaceStateController(_registry),
            new ProcessOutputService(_output),
            new LocalProcessProvider(),
            new DockerProcessProvider(),
            new ProcessOutputPump(),
            NullLogger<WorkspaceProcessManager>.Instance);
    }

    [Test]
    public async Task LaunchDockerService_WritesStderr_ToOutputRepository_OnStartupFailure()
    {
        var workspace = await RegisterDockerWorkspace("agent-up-nonexistent-image-xyz:latest");

        await _manager.LaunchApplicationAsync(workspace, "Db");

        Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
        var lines = (await _output.GetAsync(workspace.Id, "Db")).Texts();
        Assert.That(lines, Is.Not.Empty);
        Assert.That(lines, Has.Some.StartsWith("[err]"));
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_ProvidesAllWorkspacePortVariables_ToLocalProcess()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Join(worktreePath, "web"));
        Directory.CreateDirectory(Path.Join(worktreePath, "api"));

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand)
                        .At(ServerDomain.WebPath)
                        .WithPort(ServerDomain.Port().Named("WEB_PORT").On(5173))
                        .Build())
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, ServerDomain.ApiCommand)
                        .At(ServerDomain.ApiPath)
                        .WithPort(ServerDomain.Port().Named("API_PORT").On(3001))
                        .Build())
                .Build());

            var web = workspace.Applications.Single(app => app.Name == "Web");
            var api = workspace.Applications.Single(app => app.Name == "Api");

            var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, web);

            Assert.That(startInfo.WorkingDirectory, Is.Not.EqualTo(Path.Join(workspace.WorktreePath, "web")));
            Assert.That(startInfo.ArgumentList[0], Is.EqualTo("--prefix"));
            Assert.That(startInfo.ArgumentList[1], Is.EqualTo(Path.Join(workspace.WorktreePath, "web")));
            Assert.That(startInfo.Environment["WEB_PORT"], Is.EqualTo(web.AllocatedPorts.Single().AllocatedPort.ToString()));
            Assert.That(startInfo.Environment["API_PORT"], Is.EqualTo(api.AllocatedPorts.Single().AllocatedPort.ToString()));
            Assert.That(startInfo.Environment["AGENT_UP_AUDIT_ENDPOINT"], Is.EqualTo("http://127.0.0.1:5000/api/audit/record"));
            Assert.That(startInfo.Environment["AGENT_UP_WORKSPACE_ID"], Is.EqualTo(workspace.Id));
            Assert.That(startInfo.Environment["AGENT_UP_APPLICATION"], Is.EqualTo("Web"));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_UsesEachApplicationsOwnPort_WhenPortVariablesCollide()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Join(worktreePath, "mobile"));
        Directory.CreateDirectory(Path.Join(worktreePath, "web"));

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder("Mobile", "npm run web")
                        .At("mobile")
                        .WithPort(ServerDomain.Port().Named("WEB_PORT").On(8081))
                        .Build())
                .WithApplication(new ApplicationDefinitionBuilder("Example Web", ServerDomain.WebCommand)
                        .At(ServerDomain.WebPath)
                        .WithPort(ServerDomain.Port().Named("WEB_PORT").On(5600))
                        .Build())
                .Build());

            var mobile = workspace.Applications.Single(app => app.Name == "Mobile");
            var exampleWeb = workspace.Applications.Single(app => app.Name == "Example Web");

            var mobileStartInfo = new LocalProcessProvider().CreateStartInfo(workspace, mobile);
            var exampleWebStartInfo = new LocalProcessProvider().CreateStartInfo(workspace, exampleWeb);

            Assert.That(mobileStartInfo.Environment["WEB_PORT"], Is.EqualTo(mobile.AllocatedPorts.Single().AllocatedPort.ToString()));
            Assert.That(exampleWebStartInfo.Environment["WEB_PORT"], Is.EqualTo(exampleWeb.AllocatedPorts.Single().AllocatedPort.ToString()));
            Assert.That(mobileStartInfo.Environment["WEB_PORT"], Is.Not.EqualTo(exampleWebStartInfo.Environment["WEB_PORT"]));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_LoadsApplicationEnvironmentFilesAndInlineEnvironment()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        await File.WriteAllTextAsync(Path.Join(worktreePath, ".env"), """
            SECRET_PASSWORD=from-file
            SHARED_VALUE=from-file
            QUOTED_VALUE="with spaces"
            export EXPORTED_VALUE=true
            """);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "printenv")
                        .WithPort(ServerDomain.Port().Named("WEB_PORT").On(5173))
                        .WithEnvironment(new Dictionary<string, string>
                        {
                            ["SHARED_VALUE"] = "from-inline",
                            ["INLINE_ONLY"] = "true",
                            ["WEB_PORT"] = "from-inline"
                        })
                        .WithEnvironmentFile(".env")
                        .Build())
                .Build());

            var app = workspace.Applications.Single();
            var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, app);

            Assert.That(startInfo.Environment["SECRET_PASSWORD"], Is.EqualTo("from-file"));
            Assert.That(startInfo.Environment["SHARED_VALUE"], Is.EqualTo("from-inline"));
            Assert.That(startInfo.Environment["INLINE_ONLY"], Is.EqualTo("true"));
            Assert.That(startInfo.Environment["QUOTED_VALUE"], Is.EqualTo("with spaces"));
            Assert.That(startInfo.Environment["EXPORTED_VALUE"], Is.EqualTo("true"));
            Assert.That(startInfo.Environment["WEB_PORT"], Is.EqualTo(app.AllocatedPorts.Single().AllocatedPort.ToString()));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_RunsPlainExecutablesFromApplicationPath()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Join(worktreePath, "marketing-site"));

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "node marketing-site/server.mjs")
                        .Build())
                .Build());

            var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, workspace.Applications.Single());

            Assert.That(startInfo.FileName, Is.EqualTo("node"));
            Assert.That(startInfo.WorkingDirectory, Is.Not.EqualTo(workspace.WorktreePath));
            Assert.That(Directory.ResolveLinkTarget(Path.GetDirectoryName(Path.GetDirectoryName(startInfo.ArgumentList[0])!)!, returnFinalTarget: true)!.FullName,
                Is.EqualTo(workspace.WorktreePath));
            Assert.That(startInfo.ArgumentList[0], Does.EndWith(Path.Join("marketing-site", "server.mjs")));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_UsesValidatedExecutableAndArgumentList()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "npm run \"dev server\"").Build())
            .Build());

        var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo.FileName, Is.EqualTo("npm"));
        Assert.That(startInfo.WorkingDirectory, Is.Not.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList[0], Is.EqualTo("--prefix"));
        Assert.That(startInfo.ArgumentList[1], Is.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList.Skip(2), Is.EqualTo(new[] { "run", "dev server" }));
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_QualifiesDotnetProjectPathWithoutWorkspaceWorkingDirectory()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, "dotnet run --project src/Api/Api.csproj --no-launch-profile")
                    .Build())
            .Build());

        var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo.FileName, Is.EqualTo("dotnet"));
        Assert.That(startInfo.WorkingDirectory, Is.Not.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList[0], Is.EqualTo("run"));
        Assert.That(startInfo.ArgumentList[1], Is.EqualTo("--project"));
        Assert.That(Directory.ResolveLinkTarget(
                Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(startInfo.ArgumentList[2])!)!)!,
                returnFinalTarget: true)!.FullName,
            Is.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList[2], Does.EndWith(Path.Join("src", "Api", "Api.csproj")));
        Assert.That(startInfo.ArgumentList[3], Is.EqualTo("--no-launch-profile"));
    }

    [Test]
    public async Task CreateInstallStartInfo_QualifiesDotnetBuildProjectPathWithoutWorkspaceWorkingDirectory()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, "dotnet run --project src/Api/Api.csproj --no-launch-profile")
                    .WithInstall("dotnet build src/Api/Api.csproj --nologo --no-incremental")
                    .Build())
            .Build());

        var startInfo = new LocalProcessProvider().CreateInstallStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo, Is.Not.Null);
        Assert.That(startInfo!.FileName, Is.EqualTo("dotnet"));
        Assert.That(startInfo.WorkingDirectory, Is.Not.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList[0], Is.EqualTo("build"));
        Assert.That(Directory.ResolveLinkTarget(
                Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(startInfo.ArgumentList[1])!)!)!,
                returnFinalTarget: true)!.FullName,
            Is.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList[1], Does.EndWith(Path.Join("src", "Api", "Api.csproj")));
        Assert.That(startInfo.ArgumentList[2], Is.EqualTo("--nologo"));
        Assert.That(startInfo.ArgumentList[3], Is.EqualTo("--no-incremental"));
    }

    [Test]
    public async Task CreateInstallStartInfo_KeepsARootedDotnetBuildProjectPath()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, ServerDomain.ApiCommand)
                    .WithInstall("dotnet build /tmp/App.csproj --nologo")
                    .Build())
            .Build());

        var startInfo = new LocalProcessProvider().CreateInstallStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo!.ArgumentList[1], Is.EqualTo("/tmp/App.csproj"));
    }

    [Test]
    public async Task CreateInstallStartInfo_AppendsTheWorkingDirectoryWhenDotnetBuildHasNoProject()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, ServerDomain.ApiCommand)
                    .WithInstall("dotnet build --nologo")
                    .Build())
            .Build());

        var startInfo = new LocalProcessProvider().CreateInstallStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo!.ArgumentList[0], Is.EqualTo("build"));
        Assert.That(startInfo.ArgumentList[1], Is.EqualTo("--nologo"));
        Assert.That(startInfo.ArgumentList[2], Does.Contain(Path.Join("AgentUp", "WorkspaceDirectories")));
    }

    [Test, CancelAfter(120000)]
    public async Task CreateInstallStartInfo_DotnetBuild_WritesRuntimeConfigWhenWorkingDirectoryIsNotTheProject()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString("N"));
        var projectDir = Path.Join(worktreePath, "app");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Join(projectDir, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Join(projectDir, "Program.cs"), "System.Threading.Thread.Sleep(Timeout.Infinite);");
        var outputDir = Path.Join(projectDir, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Join(outputDir, "App.dll"), "");

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder("App", "dotnet run --project app/App.csproj --no-launch-profile")
                        .WithInstall("dotnet build app/App.csproj --nologo --no-incremental")
                        .Build())
                .Build());
            var startInfo = new LocalProcessProvider().CreateInstallStartInfo(workspace, workspace.Applications.Single());
            Assert.That(startInfo, Is.Not.Null);

            using var process = new Process { StartInfo = startInfo! };
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.That(process.ExitCode, Is.EqualTo(0), $"{stdout}{stderr}");
            Assert.That(File.Exists(Path.Join(outputDir, "App.runtimeconfig.json")), Is.True, $"{stdout}{stderr}");
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateInstallStartInfo_QualifiesDotnetRestoreProjectPathWithoutWorkspaceWorkingDirectory()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, "dotnet run --project src/Api/Api.csproj --no-launch-profile")
                    .WithInstall("dotnet restore src/Api/Api.csproj --nologo")
                    .Build())
            .Build());

        var startInfo = new LocalProcessProvider().CreateInstallStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo, Is.Not.Null);
        Assert.That(startInfo!.FileName, Is.EqualTo("dotnet"));
        Assert.That(startInfo.WorkingDirectory, Is.Not.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList[0], Is.EqualTo("restore"));
        Assert.That(Directory.ResolveLinkTarget(
                Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(startInfo.ArgumentList[1])!)!)!,
                returnFinalTarget: true)!.FullName,
            Is.EqualTo(workspace.WorktreePath));
        Assert.That(startInfo.ArgumentList[1], Does.EndWith(Path.Join("src", "Api", "Api.csproj")));
        Assert.That(startInfo.ArgumentList[2], Is.EqualTo("--nologo"));
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_RuntimeDisplayOverridesTheSessionDisplay()
    {
        var previousDisplay = Environment.GetEnvironmentVariable("DISPLAY");
        var previousWayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        var previousRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        try
        {
            Environment.SetEnvironmentVariable("DISPLAY", ":0");
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", "/run/user/1000");

            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .WithApplication(new ApplicationDefinitionBuilder("Editor", "printenv").Build())
                .Build());
            var app = workspace.Applications.Single();
            app.RuntimeEnvironment = new Dictionary<string, string>
            {
                ["DISPLAY"] = ":4242",
                ["WAYLAND_DISPLAY"] = "agentup-hosted-no-wayland",
                ["XDG_RUNTIME_DIR"] = "/tmp/agentup-desktop-fake",
                ["GDK_BACKEND"] = "x11"
            };

            var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, app);

            Assert.Multiple(() =>
            {
                Assert.That(startInfo.Environment["DISPLAY"], Is.EqualTo(":4242"));
                Assert.That(startInfo.Environment["WAYLAND_DISPLAY"], Is.EqualTo("agentup-hosted-no-wayland"));
                Assert.That(startInfo.Environment["XDG_RUNTIME_DIR"], Is.EqualTo("/tmp/agentup-desktop-fake"));
                Assert.That(startInfo.Environment["GDK_BACKEND"], Is.EqualTo("x11"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISPLAY", previousDisplay);
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", previousWayland);
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", previousRuntime);
        }
    }

    [Test]
    public async Task CreateInstallStartInfo_ReturnsNull_WhenInstallNotConfigured()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).Build())
            .Build());

        var startInfo = new LocalProcessProvider().CreateInstallStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo, Is.Null);
    }

    [Test]
    public async Task CreateInstallStartInfo_UsesSameWorkingDirectoryAndAllowlistAsCommand()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand)
                .At(ServerDomain.WebPath)
                .WithInstall("npm install")
                .Build())
            .Build());

        var startInfo = new LocalProcessProvider().CreateInstallStartInfo(workspace, workspace.Applications.Single());

        Assert.That(startInfo, Is.Not.Null);
        Assert.That(startInfo!.FileName, Is.EqualTo("npm"));
        Assert.That(startInfo.ArgumentList[0], Is.EqualTo("--prefix"));
        Assert.That(startInfo.ArgumentList[1], Is.EqualTo(Path.Join(workspace.WorktreePath, "web")));
        Assert.That(startInfo.ArgumentList.Skip(2), Is.EqualTo(new[] { "install" }));
    }

    [Test]
    public async Task LaunchApplicationAsync_RunsInstallStepBeforeCommand_AndPrefixesInstallOutput()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "printenv")
                        .WithInstall("printenv")
                        .Build())
                .Build());

            await _manager.LaunchApplicationAsync(workspace, "Web");

            // Applications start as Stopped, so waiting for that state returns before the
            // install+command processes have finished. Poll until the application command's
            // own (unprefixed) output shows up. LaunchApplicationAsync holds Exited until
            // stdout readers are attached and flushes those writes before disposing, so a
            // fast command such as printenv still records its lines here.
            var lines = await WaitForOutputAsync(workspace.Id, "Web",
                candidate => candidate.Any(line => !line.StartsWith("[install]", StringComparison.Ordinal)));

            // printenv always emits at least one line, so both the install step and the
            // application command must have produced output; the install's lines carry the
            // [install] prefix and must all come before the application command's own.
            var lastInstallIndex = lines.FindLastIndex(line => line.StartsWith("[install]", StringComparison.Ordinal));
            var firstCommandIndex = lines.FindIndex(line => !line.StartsWith("[install]", StringComparison.Ordinal));
            Assert.That(lastInstallIndex, Is.GreaterThanOrEqualTo(0),
                "Expected [install] output before the application command. Lines: " + string.Join(" | ", lines));
            Assert.That(firstCommandIndex, Is.GreaterThanOrEqualTo(0),
                "Expected unprefixed application output after install. Lines: " + string.Join(" | ", lines));
            Assert.That(lastInstallIndex, Is.LessThan(firstCommandIndex));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task LaunchApplicationAsync_FailsWithoutLaunchingCommand_WhenInstallExitsNonZero()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                // A missing module reliably exits 1 without needing shell metacharacters
                // (parentheses, semicolons, ...) that the command allowlist rejects.
                .WithApplication(
                    new ApplicationDefinitionBuilder(ServerDomain.WebName, "printenv")
                        .WithInstall("python3 -m agentup_test_nonexistent_module")
                        .Build())
                .Build());

            await _manager.LaunchApplicationAsync(workspace, "Web");

            var state = await WaitForApplicationStateAsync(workspace.Id, "Web", ApplicationState.Failed);
            Assert.That(state, Is.EqualTo(ApplicationState.Failed));

            // Only install-prefixed output should exist; the application's own printenv must
            // never have run.
            var lines = (await _output.GetAsync(workspace.Id, "Web")).Texts();
            Assert.That(lines, Has.All.Matches<string>(line => line.StartsWith("[install]", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task LaunchApplicationAsync_MarksApplicationFailed_WhenInstallCommandIsInvalid()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "printenv")
                    .WithInstall("npm install; rm -rf /")
                    .Build())
            .Build());

        await _manager.LaunchApplicationAsync(workspace, "Web");

        var state = await WaitForApplicationStateAsync(workspace.Id, "Web", ApplicationState.Failed);
        Assert.That(state, Is.EqualTo(ApplicationState.Failed));

        var lines = (await _output.GetAsync(workspace.Id, "Web")).Texts();
        Assert.That(lines, Has.Some.Contains("not a shell expression"));
    }

    [Test]
    public async Task LaunchApplicationAsync_DoesNotLaunchCommand_WhenKilledDuringInstall()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                // Never exits on its own, so it is still running (and killable) when
                // KillApplicationAsync fires below.
                .WithApplication(
                    new ApplicationDefinitionBuilder(ServerDomain.WebName, "printenv")
                        .WithInstall("python3 -m http.server 0 --bind 127.0.0.1")
                        .Build())
                .Build());

            var launchTask = _manager.LaunchApplicationAsync(workspace, "Web");
            await Task.Delay(500);
            await _manager.KillApplicationAsync(workspace.Id, "Web");

            Assert.ThrowsAsync<InvalidOperationException>(async () => await launchTask);

            // Give the application command a window in which it would have started if the
            // kill had not actually stopped the install.
            await Task.Delay(1000);

            // Only install-prefixed output (if any) should exist; the application's own
            // printenv must never have run.
            var lines = (await _output.GetAsync(workspace.Id, "Web")).Texts();
            Assert.That(lines, Has.All.Matches<string>(line => line.StartsWith("[install]", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_RejectsShellExpressions()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "npm run dev; rm -rf /").Build())
            .Build());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new LocalProcessProvider().CreateStartInfo(workspace, workspace.Applications.Single()));

        Assert.That(ex!.Message, Does.Contain("not a shell expression"));
    }

    [Test]
    public async Task CreateLocalProcessStartInfo_LoadsNestedEnvironmentFilesUnderWorkspaceRoot()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        var envDirectory = Path.Join(worktreePath, "config");
        Directory.CreateDirectory(envDirectory);
        await File.WriteAllTextAsync(Path.Join(envDirectory, ".env.local"), "SECRET_PASSWORD=from-nested-file");

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "printenv")
                        .WithEnvironmentFile("config/.env.local")
                        .Build())
                .Build());

            var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, workspace.Applications.Single());

            Assert.That(startInfo.Environment["SECRET_PASSWORD"], Is.EqualTo("from-nested-file"));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public void Register_RejectsEnvironmentFilesOutsideWorkspaceRoot()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            _registry.RegisterAsync(ServerDomain.Workspace()
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "printenv")
                        .WithEnvironmentFile("../.env")
                        .Build())
                .Build()));

        Assert.That(ex!.Message, Does.Contain("must stay under the workspace root"));
    }

    [Test]
    public void CreateWorkspaceDirectoryAlias_RejectsExistingDirectoryThatIsNotTheWorkspaceAlias()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        var workingDirectory = Path.Join(root, "workspace");
        var aliasRoot = Path.Join(root, "aliases");

        try
        {
            Directory.CreateDirectory(workingDirectory);
            Directory.CreateDirectory(LocalProcessProvider.WorkspaceDirectoryAliasPath(workingDirectory, aliasRoot));

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LocalProcessProvider.CreateWorkspaceDirectoryAlias(workingDirectory, aliasRoot));

            Assert.That(ex!.Message, Does.Contain("could not be verified"));
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task KillApplicationAsync_marksIntentionalLocalProcessExitAsStopped()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, LongRunningStopCommand())
                        .Build())
                .Build());

            await _manager.LaunchApplicationAsync(workspace, "Web");
            await _registry.UpdateApplicationStateAsync(workspace.Id, "Web", ApplicationState.Running);

            await _manager.KillApplicationAsync(workspace.Id, "Web");

            var state = await WaitForApplicationStateAsync(workspace.Id, "Web", ApplicationState.Stopped);
            Assert.That(state, Is.EqualTo(ApplicationState.Stopped));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task LaunchApplicationAsync_recordsFastNonZeroExitAsFailed()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "python3 -c \"raise Exception\"")
                        .Build())
                .Build());

            await _manager.LaunchApplicationAsync(workspace, "Web");

            var state = await WaitForApplicationStateAsync(workspace.Id, "Web", ApplicationState.Failed);
            Assert.That(state, Is.EqualTo(ApplicationState.Failed));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateDockerRunArguments_AddsEnvironmentFilesAndInlineEnvironment()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        await File.WriteAllTextAsync(Path.Join(worktreePath, ".env.database"), "POSTGRES_PASSWORD=secret");

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithService(new DockerServiceDefinition(
                        "Database",
                        "postgres:17",
                        Environment: new Dictionary<string, string> { ["POSTGRES_USER"] = "user" },
                        EnvironmentFiles: [".env.database"]))
                .Build());

            var args = new DockerProcessProvider().CreateRunArguments("agentup-test-db", workspace, workspace.Applications.Single());

            Assert.That(args, Does.Contain("--env-file"));
            Assert.That(args, Does.Contain(Path.Join(worktreePath, ".env.database")));
            Assert.That(args, Does.Contain("-e"));
            Assert.That(args, Does.Contain("POSTGRES_USER=user"));
            Assert.That(args, Does.Contain("AGENT_UP_AUDIT_ENDPOINT=http://host.agent-up:5000/api/audit/record"));
            Assert.That(args, Does.Contain($"AGENT_UP_WORKSPACE_ID={workspace.Id}"));
            Assert.That(args, Does.Contain("AGENT_UP_APPLICATION=Database"));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
                Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateDockerRunArguments_AddsHostGatewayAliasAndInterpolatesWorkspacePorts()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder("Roommate", "./gradlew bootRun")
                    .WithPort(ServerDomain.Port().Named("SERVER_PORT").On(8080))
                    .Build())
            .WithService(new DockerServiceDefinition(
                    "Keymaster",
                    "team-propra/keymaster:v1",
                    [ServerDomain.Port().Named("KEYMASTER_PORT").On(3000).Build()],
                    new Dictionary<string, string>
                    {
                        ["ROOMMATE_URL"] = "http://host.agent-up:${SERVER_PORT}",
                        ["ROOMMATE_ENDPOINT"] = "/api/access",
                        ["UNRESOLVED"] = "${MISSING_PORT}"
                    }))
            .Build());
        var keymaster = workspace.Applications.Single(app => app.Name == "Keymaster");
        var roommatePort = workspace.Applications
            .Single(app => app.Name == "Roommate")
            .AllocatedPorts.Single()
            .AllocatedPort;

        var args = new DockerProcessProvider().CreateRunArguments("agentup-test-keymaster", workspace, keymaster);

        Assert.That(args, Does.Contain("--add-host"));
        Assert.That(args, Does.Contain("host.agent-up:host-gateway"));
        Assert.That(args, Does.Contain($"ROOMMATE_URL=http://host.agent-up:{roommatePort}"));
        Assert.That(args, Does.Contain("ROOMMATE_ENDPOINT=/api/access"));
        Assert.That(args, Does.Contain("UNRESOLVED=${MISSING_PORT}"));
    }

    [Test]
    public async Task CreateDockerRunArguments_AppendsCommandArgumentsAfterTheImage()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithService(new DockerServiceDefinition(
                    "Redpanda",
                    "docker.redpanda.com/redpandadata/redpanda:v24.2.4",
                    Command: ["redpanda", "start", "--smp", "1"]))
            .Build());

        var args = new DockerProcessProvider().CreateRunArguments("agentup-test-redpanda", workspace, workspace.Applications.Single());

        Assert.That(args.SkipWhile(a => a != "docker.redpanda.com/redpandadata/redpanda:v24.2.4").Skip(1),
            Is.EqualTo(new[] { "redpanda", "start", "--smp", "1" }));
    }

    [Test]
    public async Task LaunchApplication_skipsDesktopAppsOffLinux()
    {
        var manager = new WorkspaceProcessManager(
            ServerTestComposition.CreateWorkspaceStateController(_registry),
            new ProcessOutputService(_output),
            new LocalProcessProvider(),
            new DockerProcessProvider(),
            new ProcessOutputPump(),
            NullLogger<WorkspaceProcessManager>.Instance,
            isLinux: () => false);
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithDesktopApplication(new DesktopApplicationDefinition("Editor", "dotnet run", "."))
            .Build());

        await manager.LaunchApplicationAsync(workspace, "Editor");

        var lines = (await _output.GetAsync(workspace.Id, "Editor")).Texts();
        Assert.Multiple(() =>
        {
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(lines, Has.Some.Contains("Linux"));
        });
    }

    [Test]
    public async Task LaunchApplicationAsync_marksFailedAndReturns_whenCapabilityCannotRun()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithDotnet(new DotnetApplicationDefinition(
                "Example API",
                "10.0.x",
                new DotnetRunDefinition("ExampleApi.csproj")))
            .Build());

        await _manager.LaunchApplicationAsync(workspace, "Example API");

        var app = workspace.Applications.Single();
        var lines = (await _output.GetAsync(workspace.Id, "Example API")).Texts();
        Assert.Multiple(() =>
        {
            Assert.That(app.State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(app.CapabilityStatus!.CanRun, Is.False);
            Assert.That(lines, Has.Some.Contains("not enabled"));
            Assert.That(_manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task LaunchAsync_launchesRemainingApplications_whenOneCannotRun()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, LongRunningStopCommand()).Build())
                .WithDotnet(new DotnetApplicationDefinition(
                    "Example API",
                    "10.0.x",
                    new DotnetRunDefinition("ExampleApi.csproj")))
                .Build());

            await _manager.LaunchAsync(workspace);

            Assert.Multiple(() =>
            {
                Assert.That(workspace.Applications.Single(app => app.Name == ServerDomain.WebName).State,
                    Is.Not.EqualTo(ApplicationState.Failed));
                Assert.That(workspace.Applications.Single(app => app.Name == "Example API").State,
                    Is.EqualTo(ApplicationState.Failed));
                Assert.That(_manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(1));
            });
        }
        finally
        {
            await _manager.KillAsync(_registry.GetAll().Single().Id);
            Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task LaunchAsync_marksFailedAndContinues_whenOneCommandIsRejected()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "npm run dev; rm -rf /").Build())
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, LongRunningStopCommand()).Build())
                .Build());

            await _manager.LaunchAsync(workspace);
            var lines = (await _output.GetAsync(workspace.Id, ServerDomain.WebName)).Texts();

            Assert.Multiple(() =>
            {
                Assert.That(workspace.Applications.Single(app => app.Name == ServerDomain.WebName).State,
                    Is.EqualTo(ApplicationState.Failed));
                Assert.That(workspace.Applications.Single(app => app.Name == ServerDomain.ApiName).State,
                    Is.Not.EqualTo(ApplicationState.Failed));
                Assert.That(lines, Has.Some.Contains("not a shell expression"));
                Assert.That(_manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(1));
            });
        }
        finally
        {
            await _manager.KillAsync(_registry.GetAll().Single().Id);
            Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task LaunchAsync_leavesNoTrackedProcess_whenEveryApplicationFails()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithDotnet(new DotnetApplicationDefinition(
                "Example API",
                "10.0.x",
                new DotnetRunDefinition("ExampleApi.csproj")))
            .WithDotnet(new DotnetApplicationDefinition(
                "Worker",
                "10.0.x",
                new DotnetRunDefinition("Worker.csproj")))
            .Build());

        await _manager.LaunchAsync(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(workspace.Applications.Select(app => app.State),
                Is.All.EqualTo(ApplicationState.Failed));
            Assert.That(_manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task LaunchApplicationAsync_throwsWhenTheApplicationIsMissing()
    {
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).Build())
            .Build());

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _manager.LaunchApplicationAsync(workspace, "missing"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task LaunchApplicationAsync_marksFailedAndReturns_whenTheExecutableCannotStart()
    {
        var manager = CreateManager(local: new MissingExecutableLocalProcessProvider());
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).Build())
            .Build());

        await manager.LaunchApplicationAsync(workspace, ServerDomain.WebName);
        var lines = (await _output.GetAsync(workspace.Id, ServerDomain.WebName)).Texts();

        Assert.Multiple(() =>
        {
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(lines, Has.Some.StartsWith("[err]"));
            Assert.That(manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task LaunchApplicationAsync_failsWithoutLaunchingCommand_whenInstallCannotStart()
    {
        var manager = CreateManager(local: new MissingInstallExecutableLocalProcessProvider());
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "printenv")
                .WithInstall("npm install")
                .Build())
            .Build());

        await manager.LaunchApplicationAsync(workspace, ServerDomain.WebName);

        Assert.Multiple(() =>
        {
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task LaunchAsync_recordsANonMissingInvalidOperationAsFailedAndContinues()
    {
        var manager = CreateManager(local: new ThrowingThenRealLocalProcessProvider(
            ServerDomain.WebName,
            new InvalidOperationException("spawn failed")));
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).Build())
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, LongRunningStopCommand()).Build())
                .Build());

            await manager.LaunchAsync(workspace);
            var lines = (await _output.GetAsync(workspace.Id, ServerDomain.WebName)).Texts();

            Assert.Multiple(() =>
            {
                Assert.That(workspace.Applications.Single(app => app.Name == ServerDomain.WebName).State,
                    Is.EqualTo(ApplicationState.Failed));
                Assert.That(workspace.Applications.Single(app => app.Name == ServerDomain.ApiName).State,
                    Is.Not.EqualTo(ApplicationState.Failed));
                Assert.That(lines, Has.Some.Contains("spawn failed"));
                Assert.That(manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(1));
            });
        }
        finally
        {
            await manager.KillAsync(_registry.GetAll().Single().Id);
            Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    public async Task LaunchAsync_rethrowsWhenLaunchReportsTheApplicationMissing()
    {
        var manager = CreateManager(local: new ThrowingThenRealLocalProcessProvider(
            ServerDomain.WebName,
            new InvalidOperationException($"Application '{ServerDomain.WebName}' not found in workspace.")));
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).Build())
            .Build());

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => manager.LaunchAsync(workspace));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task LaunchDockerService_marksFailedAndReturns_whenDockerThrowsWin32()
    {
        await AssertDockerLaunchFails(new Win32Exception("docker missing"));
    }

    [Test]
    public async Task LaunchDockerService_marksFailedAndReturns_whenDockerWrapsWin32()
    {
        await AssertDockerLaunchFails(new InvalidOperationException(
            "docker could not be started: docker missing",
            new Win32Exception("docker missing")));
    }

    [Test]
    public async Task LaunchDockerService_marksFailedAndReturns_whenDockerCannotRun()
    {
        await AssertDockerLaunchFails(new IOException("docker socket"));
    }

    [Test]
    public async Task LaunchDockerService_marksFailedAndReturns_whenRunExitsNonZero()
    {
        var manager = CreateManager(docker: new FailingRunDockerProcessProvider());
        var workspace = await RegisterDockerWorkspace();

        await manager.LaunchApplicationAsync(workspace, "Db");
        var lines = (await _output.GetAsync(workspace.Id, "Db")).Texts();

        Assert.Multiple(() =>
        {
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(lines, Has.Some.Contains("image not found"));
            Assert.That(manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task LaunchApplication_stopsTheDesktopSessionWhenTheProcessExits()
    {
        var displays = new FakeDesktopDisplayProvider();
        var desktop = new DesktopApplicationsController(new DesktopSessionService(
            displays,
            new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new FakeHostedDesktopNativeLibraryProvider(),
            NullLogger<DesktopSessionService>.Instance));
        var manager = new WorkspaceProcessManager(
            ServerTestComposition.CreateWorkspaceStateController(_registry),
            new ProcessOutputService(_output),
            new LocalProcessProvider(),
            new DockerProcessProvider(),
            new ProcessOutputPump(),
            NullLogger<WorkspaceProcessManager>.Instance,
            desktop);
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithDesktopApplication(new DesktopApplicationDefinition("Editor", "printenv", "."))
                .Build());
            var app = workspace.Applications.Single();
            app.RuntimeEnvironment = await desktop.PrepareAsync(workspace, app, CancellationToken.None);

            await manager.LaunchApplicationAsync(workspace, "Editor");
            await WaitForApplicationStateAsync(workspace.Id, "Editor", ApplicationState.Stopped);

            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
            while (!displays.Stopped && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(50);

            Assert.That(displays.Stopped, Is.True);
        }
        finally
        {
            Directory.Delete(worktreePath, recursive: true);
        }
    }

    [Test]
    [CancelAfter(60_000)]
    public async Task LaunchApplicationAsync_CapturesHighRateStdoutAndStderrWithoutSplicing()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        var script = Path.Join(worktreePath, "generator.py");
        await File.WriteAllTextAsync(script, """
            import sys
            for i in range(10000):
                if i % 2 == 0:
                    sys.stdout.write(f"out-{i:05d}\n")
                    sys.stdout.flush()
                else:
                    sys.stderr.write(f"err-{i:05d}\n")
                    sys.stderr.flush()
            sys.stdout.write("L" * 8192 + "\n")
            sys.stdout.write("final-no-nl")
            sys.stdout.flush()
            """);

        try
        {
            var executable = new[] { "python3", "python" }.FirstOrDefault(ExecutableOnPath);
            Assert.That(executable, Is.Not.Null, "python3 or python is required for the high-rate console generator.");
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, $"{executable} {script}").Build())
                .Build());

            await _manager.LaunchApplicationAsync(workspace, "Web");
            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
            var lines = (await _output.GetAsync(workspace.Id, "Web")).Texts().ToList();
            while (!lines.Contains("final-no-nl") && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
                lines = (await _output.GetAsync(workspace.Id, "Web")).Texts().ToList();
            }
            var stdout = lines.Where(line => line.StartsWith("out-", StringComparison.Ordinal)).ToList();
            var stderr = lines.Where(line => line.StartsWith("[err] err-", StringComparison.Ordinal)).ToList();
            Assert.That(stdout, Is.EqualTo(Enumerable.Range(0, 5000).Select(index => $"out-{index * 2:00000}")));
            Assert.That(stderr, Is.EqualTo(Enumerable.Range(0, 5000).Select(index => $"[err] err-{index * 2 + 1:00000}")));
            Assert.That(lines, Has.Some.EqualTo(new string('L', 8192)));
            Assert.That(lines, Has.Some.EqualTo("final-no-nl"));
            Assert.That(lines, Has.None.Contains('\n'));
        }
        finally
        {
            DeleteDirectoryIfExists(worktreePath);
        }
    }

    [Test]
    public async Task LaunchDockerService_writesMultilineStderrWhenRunFails()
    {
        var docker = new ScriptedDockerProcessProvider(
            () => throw new InvalidOperationException("log process should not start"),
            exitCode: 1,
            runResult: new DockerCommandResult(1, "", "first\r\nsecond\n"));
        var manager = CreateManager(docker: docker);
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithService(ServerDomain.PostgresService())
            .Build());

        await manager.LaunchApplicationAsync(workspace, "Db");
        var lines = (await _output.GetAsync(workspace.Id, "Db")).Texts();
        Assert.Multiple(() =>
        {
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(lines, Is.EqualTo(new[] { "[err] first", "[err] second" }));
        });
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task LaunchDockerService_capturesLogLinesAndMarksNaturalExit()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        var script = Path.Join(worktreePath, "docker-log.py");
        await File.WriteAllTextAsync(script, """
            import sys
            sys.stdout.write("docker-out\n")
            sys.stdout.flush()
            sys.stderr.write("docker-err\n")
            sys.stderr.flush()
            """);
        var docker = new ScriptedDockerProcessProvider(() => CreateScriptProcess(script), exitCode: 0);
        var manager = CreateManager(docker: docker);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .WithService(ServerDomain.PostgresService())
                .Build());

            await manager.LaunchApplicationAsync(workspace, "Db");
            var lines = await WaitForOutputAsync(
                workspace.Id,
                "Db",
                current => current.Contains("docker-out") && current.Contains("[err] docker-err"));
            var state = await WaitForApplicationStateAsync(workspace.Id, "Db", ApplicationState.Stopped, TimeSpan.FromSeconds(10));

            Assert.That(state, Is.EqualTo(ApplicationState.Stopped));
            Assert.That(lines, Does.Contain("docker-out"));
            Assert.That(lines, Does.Contain("[err] docker-err"));
        }
        finally
        {
            DeleteDirectoryIfExists(worktreePath);
        }
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task LaunchDockerService_ignoresLogExitAfterKill()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        var script = Path.Join(worktreePath, "docker-sleep.py");
        await File.WriteAllTextAsync(script, "import time\ntime.sleep(30)\n");
        var docker = new ScriptedDockerProcessProvider(() => CreateScriptProcess(script), exitCode: 1);
        var manager = CreateManager(docker: docker);
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithService(ServerDomain.PostgresService())
            .Build());

        try
        {
            await manager.LaunchApplicationAsync(workspace, "Db");
            await manager.KillApplicationAsync(workspace.Id, "Db");

            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Stopped));
            Assert.That(docker.RemovedAfterKill, Is.True);
        }
        finally
        {
            DeleteDirectoryIfExists(worktreePath);
        }
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task LaunchDockerService_continuesAfterAppendFailuresWhileFlushing()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        var script = Path.Join(worktreePath, "docker-log.py");
        await File.WriteAllTextAsync(script, "import sys\nsys.stdout.write('line\\n')\nsys.stdout.flush()\n");
        var output = new AppendThrowsOutputRepository();
        var docker = new ScriptedDockerProcessProvider(() => CreateScriptProcess(script), exitCode: 1);
        var manager = new WorkspaceProcessManager(
            ServerTestComposition.CreateWorkspaceStateController(_registry),
            new ProcessOutputService(output),
            new LocalProcessProvider(),
            docker,
            new ProcessOutputPump(),
            NullLogger<WorkspaceProcessManager>.Instance);
        var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithService(ServerDomain.PostgresService())
            .Build());

        try
        {
            await manager.LaunchApplicationAsync(workspace, "Db");
            var state = await WaitForApplicationStateAsync(workspace.Id, "Db", ApplicationState.Failed, TimeSpan.FromSeconds(10));

            Assert.That(state, Is.EqualTo(ApplicationState.Failed));
        }
        finally
        {
            DeleteDirectoryIfExists(worktreePath);
        }
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task LaunchApplicationAsync_swallowsPumpReadFailures()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        var script = Path.Join(worktreePath, "hello.py");
        await File.WriteAllTextAsync(script, "print('hello')\n");
        var manager = new WorkspaceProcessManager(
            ServerTestComposition.CreateWorkspaceStateController(_registry),
            new ProcessOutputService(_output),
            new LocalProcessProvider(),
            new DockerProcessProvider(),
            new ThrowingOutputPump(),
            NullLogger<WorkspaceProcessManager>.Instance);
        var executable = new[] { "python3", "python" }.FirstOrDefault(ExecutableOnPath);
        Assert.That(executable, Is.Not.Null);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, $"{executable} {script}").Build())
                .Build());

            await manager.LaunchApplicationAsync(workspace, "Web");
            var state = await WaitForApplicationStateAsync(workspace.Id, "Web", ApplicationState.Stopped, TimeSpan.FromSeconds(10));

            Assert.That(state, Is.EqualTo(ApplicationState.Stopped));
        }
        finally
        {
            DeleteDirectoryIfExists(worktreePath);
        }
    }

    [Test]
    [CancelAfter(30_000)]
    public async Task LaunchApplicationAsync_swallowsCancelledPumpReads()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(worktreePath);
        var script = Path.Join(worktreePath, "hello.py");
        await File.WriteAllTextAsync(script, "print('hello')\n");
        var manager = new WorkspaceProcessManager(
            ServerTestComposition.CreateWorkspaceStateController(_registry),
            new ProcessOutputService(_output),
            new LocalProcessProvider(),
            new DockerProcessProvider(),
            new CancelledOutputPump(),
            NullLogger<WorkspaceProcessManager>.Instance);
        var executable = new[] { "python3", "python" }.FirstOrDefault(ExecutableOnPath);
        Assert.That(executable, Is.Not.Null);

        try
        {
            var workspace = await _registry.RegisterAsync(ServerDomain.Workspace()
                .At(worktreePath)
                .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, $"{executable} {script}").Build())
                .Build());

            await manager.LaunchApplicationAsync(workspace, "Web");
            var state = await WaitForApplicationStateAsync(workspace.Id, "Web", ApplicationState.Stopped, TimeSpan.FromSeconds(10));

            Assert.That(state, Is.EqualTo(ApplicationState.Stopped));
        }
        finally
        {
            DeleteDirectoryIfExists(worktreePath);
        }
    }

    private WorkspaceProcessManager CreateManager(
        ILocalProcessProvider? local = null,
        IDockerProcessProvider? docker = null)
        => new(
            ServerTestComposition.CreateWorkspaceStateController(_registry),
            new ProcessOutputService(_output),
            local ?? new LocalProcessProvider(),
            docker ?? new DockerProcessProvider(),
            new ProcessOutputPump(),
            NullLogger<WorkspaceProcessManager>.Instance);

    private static Process CreateScriptProcess(string scriptPath)
    {
        var executable = new[] { "python3", "python" }.First(ExecutableOnPath);
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                ArgumentList = { "-u", scriptPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            },
            EnableRaisingEvents = false
        };
    }

    private static string LongRunningStopCommand()
    {
        var executable = new[] { "python3", "python" }.FirstOrDefault(ExecutableOnPath);
        Assert.That(executable, Is.Not.Null, "python3 or python is required to cover an intentional local process stop.");
        return $"{executable} -m http.server 0 --bind 127.0.0.1";
    }

    private static bool ExecutableOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return false;

        var suffix = OperatingSystem.IsWindows() ? ".exe" : "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Join(directory, name + suffix))
                              || File.Exists(Path.Join(directory, name)));
    }

    private async Task<ApplicationState> WaitForApplicationStateAsync(
        string workspaceId,
        string appName,
        ApplicationState expected,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        var state = _registry.GetById(workspaceId)!.Applications.Single(app => app.Name == appName).State;
        while (state != expected && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
            state = _registry.GetById(workspaceId)!.Applications.Single(app => app.Name == appName).State;
        }

        return state;
    }

    private async Task<List<string>> WaitForOutputAsync(
        string workspaceId,
        string appName,
        Func<List<string>, bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        var lines = (await _output.GetAsync(workspaceId, appName)).Texts().ToList();
        while (!condition(lines) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
            lines = (await _output.GetAsync(workspaceId, appName)).Texts().ToList();
        }

        return lines;
    }

    private static void DeleteDirectoryIfExists(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private async Task<Workspace> RegisterDockerWorkspace(string image = "postgres:16")
        => await _registry.RegisterAsync(ServerDomain.Workspace()
            .WithService(new DockerServiceDefinition("Db", image))
            .Build());

    private async Task AssertDockerLaunchFails(Exception error)
    {
        var manager = CreateManager(docker: new ThrowingDockerProcessProvider(error));
        var workspace = await RegisterDockerWorkspace();

        await manager.LaunchApplicationAsync(workspace, "Db");
        var lines = (await _output.GetAsync(workspace.Id, "Db")).Texts();

        Assert.Multiple(() =>
        {
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(lines, Has.Some.StartsWith("[err]"));
            Assert.That(manager.GetRuntime(workspace.Id).ProcessCount, Is.EqualTo(0));
        });
    }

    private static Process MissingExecutableProcess()
        => new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "agent-up-missing"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

    private sealed class MissingExecutableLocalProcessProvider : ILocalProcessProvider
    {
        public Process CreateApplicationProcess(Workspace workspace, ApplicationInstance app)
            => MissingExecutableProcess();

        public Process? CreateInstallProcess(Workspace workspace, ApplicationInstance app) => null;

        public void Kill(Process process) => process.Kill(entireProcessTree: true);
    }

    private sealed class MissingInstallExecutableLocalProcessProvider : ILocalProcessProvider
    {
        public Process CreateApplicationProcess(Workspace workspace, ApplicationInstance app)
            => throw new InvalidOperationException("application command must not launch after a failed install");

        public Process? CreateInstallProcess(Workspace workspace, ApplicationInstance app)
            => MissingExecutableProcess();

        public void Kill(Process process) => process.Kill(entireProcessTree: true);
    }

    private sealed class ThrowingThenRealLocalProcessProvider : ILocalProcessProvider
    {
        private readonly LocalProcessProvider _inner = new();
        private readonly string _failingName;
        private readonly Exception _error;

        public ThrowingThenRealLocalProcessProvider(string failingName, Exception error)
        {
            _failingName = failingName;
            _error = error;
        }

        public Process CreateApplicationProcess(Workspace workspace, ApplicationInstance app)
        {
            if (app.Name == _failingName)
                throw _error;
            return _inner.CreateApplicationProcess(workspace, app);
        }

        public Process? CreateInstallProcess(Workspace workspace, ApplicationInstance app)
            => _inner.CreateInstallProcess(workspace, app);

        public void Kill(Process process) => _inner.Kill(process);
    }

    private sealed class ThrowingDockerProcessProvider : IDockerProcessProvider
    {
        private readonly Exception _error;

        public ThrowingDockerProcessProvider(Exception error) => _error = error;

        public string GetContainerName(string workspaceId, string appName) => "agentup-test-db";

        public IReadOnlyList<string> CreateRunArguments(
            string containerName,
            Workspace workspace,
            ApplicationInstance app)
            => [];

        public Process CreateLogProcess(string containerName)
            => throw new NotSupportedException();

        public Task<DockerCommandResult> RunAsync(params string[] args) => throw _error;

        public Task<int> GetExitCodeAsync(string containerName) => throw _error;
    }

    private sealed class FailingRunDockerProcessProvider : IDockerProcessProvider
    {
        public string GetContainerName(string workspaceId, string appName) => "agentup-test-db";

        public IReadOnlyList<string> CreateRunArguments(
            string containerName,
            Workspace workspace,
            ApplicationInstance app)
            => ["run", "--name", containerName, "missing:latest"];

        public Process CreateLogProcess(string containerName)
            => throw new NotSupportedException();

        public Task<DockerCommandResult> RunAsync(params string[] args)
            => Task.FromResult(args is ["rm", ..]
                ? new DockerCommandResult(0, "", "")
                : new DockerCommandResult(1, "", "image not found\n"));

        public Task<int> GetExitCodeAsync(string containerName) => Task.FromResult(1);
    }

    private sealed class ScriptedDockerProcessProvider : IDockerProcessProvider
    {
        private readonly Func<Process> _createLog;
        private readonly int _exitCode;
        private readonly DockerCommandResult _runResult;

        public ScriptedDockerProcessProvider(
            Func<Process> createLog,
            int exitCode,
            DockerCommandResult? runResult = null)
        {
            _createLog = createLog;
            _exitCode = exitCode;
            _runResult = runResult ?? new DockerCommandResult(0, "", "");
        }

        public int RemoveCount { get; private set; }

        public bool RemovedAfterKill => RemoveCount >= 2;

        public string GetContainerName(string workspaceId, string appName)
            => $"agentup-{appName}";

        public IReadOnlyList<string> CreateRunArguments(
            string containerName,
            Workspace workspace,
            ApplicationInstance app)
            => ["run", app.Image ?? "image"];

        public Process CreateLogProcess(string containerName)
            => _createLog();

        public Task<DockerCommandResult> RunAsync(params string[] args)
        {
            if (args is ["rm", "-f", _])
            {
                RemoveCount++;
                return Task.FromResult(new DockerCommandResult(0, "", ""));
            }

            return Task.FromResult(_runResult);
        }

        public Task<int> GetExitCodeAsync(string containerName)
            => Task.FromResult(_exitCode);
    }

    private sealed class AppendThrowsOutputRepository : IOutputRepository
    {
        public Task AppendAsync(
            string workspaceId,
            string appName,
            ProcessOutputLine line,
            CancellationToken ct = default)
            => throw new InvalidOperationException("disk full");

        public Task<IReadOnlyList<ProcessOutputLine>> GetAsync(
            string workspaceId,
            string appName,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProcessOutputLine>>([]);

        public Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingOutputPump : IProcessOutputPump
    {
        public Task DrainAsync(
            StreamReader reader,
            ProcessOutputStream stream,
            Func<string, ProcessOutputStream, CancellationToken, Task> onLine,
            CancellationToken cancellationToken = default)
        {
            Exception error = stream == ProcessOutputStream.Stdout
                ? new IOException("stdout closed")
                : new ObjectDisposedException(nameof(StreamReader));
            return Task.FromException(error);
        }
    }

    private sealed class CancelledOutputPump : IProcessOutputPump
    {
        public Task DrainAsync(
            StreamReader reader,
            ProcessOutputStream stream,
            Func<string, ProcessOutputStream, CancellationToken, Task> onLine,
            CancellationToken cancellationToken = default)
            => Task.FromCanceled(new CancellationToken(canceled: true));
    }
}
