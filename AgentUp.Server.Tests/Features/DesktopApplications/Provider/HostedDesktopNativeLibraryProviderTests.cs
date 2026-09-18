using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Processes.Providers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Provider;

[TestFixture]
[Platform(Include = "Linux")]
public sealed class HostedDesktopNativeLibraryProviderTests
{
    [Test]
    public void MergeLibraryPath_DedupesAndPreservesOrder()
    {
        var merged = HostedDesktopNativeLibraryProvider.MergeLibraryPath(
            "/nix/store/a/lib:/nix/store/b/lib",
            "/nix/store/b/lib:/usr/lib",
            null,
            "");

        Assert.That(merged, Is.EqualTo("/nix/store/a/lib:/nix/store/b/lib:/usr/lib"));
    }

    [Test]
    public void FindShellNix_WalksUpFromANestedWorkspacePath()
    {
        var root = Path.Join(Path.GetTempPath(), "agentup-shell-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Join(root, "Examples", "linux-desktop");
        Directory.CreateDirectory(nested);
        var shellNix = Path.Join(root, "shell.nix");
        File.WriteAllText(shellNix, "{ pkgs ? import <nixpkgs> {} }: pkgs.mkShell {}");

        try
        {
            Assert.That(HostedDesktopNativeLibraryProvider.FindShellNix(nested), Is.EqualTo(shellNix));
            Assert.That(HostedDesktopNativeLibraryProvider.FindShellNix(shellNix), Is.EqualTo(shellNix));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void FindShellNix_skipsEmptyInvalidAndUnrelatedRoots()
    {
        var empty = Path.Join(Path.GetTempPath(), "agentup-noshell-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(HostedDesktopNativeLibraryProvider.FindShellNix(null, " ", "\0"), Is.Null);
                Assert.That(HostedDesktopNativeLibraryProvider.FindShellNix(empty), Is.Null);
            });
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Test, CancelAfter(180000)]
    public void CreateEnvironment_ResolvesSkiaSharpNativeDependenciesWhenTheServerHasNoInheritedLibraryPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
            Assert.Ignore("Examples/linux-desktop was not found next to agent-up.sln.");

        EnsureLinuxDesktopBuilt(repositoryRoot);
        var nativeLibrary = FindLibSkiaSharp(repositoryRoot);
        Assert.That(nativeLibrary, Is.Not.Null, "dotnet build did not produce libSkiaSharp.so.");

        var missingWithout = TryLddMissing(nativeLibrary!, "");
        if (missingWithout is null)
        {
            Assert.Ignore("ldd is not available to inspect SkiaSharp native dependencies.");
            return;
        }

        var environment = new HostedDesktopNativeLibraryProvider(NoInheritedNativeLibraries)
            .CreateEnvironment(repositoryRoot);
        if (missingWithout.Count == 0)
            return;

        Assert.That(environment.ContainsKey("LD_LIBRARY_PATH"), Is.True,
            "Hosted desktop processes need native libraries that are not on the default linker path: "
            + string.Join(", ", missingWithout));
        Assert.That(TryLddMissing(nativeLibrary!, environment["LD_LIBRARY_PATH"]), Is.Empty);
    }

    [Test, CancelAfter(180000)]
    public async Task PrepareAsync_StartsTheSampleAvaloniaAppWhenTheServerHasNoInheritedLibraryPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
            Assert.Ignore("Examples/linux-desktop was not found next to agent-up.sln.");

        EnsureLinuxDesktopBuilt(repositoryRoot);
        PreloadHostDisplayLibraries(repositoryRoot);

        var displays = new LinuxX11DesktopDisplayProvider(new PngFrameProvider());
        var controller = new DesktopApplicationsController(new DesktopSessionService(
            displays,
            new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new HostedDesktopNativeLibraryProvider(NoInheritedNativeLibraries),
            NullLogger<DesktopSessionService>.Instance));
        var workspace = new Workspace
        {
            Id = "hosted-skia",
            DisplayName = "hosted-skia",
            RepositoryPath = repositoryRoot,
            WorktreePath = repositoryRoot,
            Branch = "main",
            Commit = "test",
            Applications =
            [
                new ApplicationInstance
                {
                    Name = "Sample Desktop",
                    Command = "dotnet run --project Examples/linux-desktop/LinuxDesktop.csproj --no-launch-profile",
                    Install = "dotnet build Examples/linux-desktop/LinuxDesktop.csproj --nologo --no-incremental",
                    Path = ".",
                    Kind = ApplicationKind.Desktop,
                    DesktopWidth = 800,
                    DesktopHeight = 600
                }
            ]
        };
        var application = workspace.Applications[0];

        IReadOnlyDictionary<string, string> environment;
        try
        {
            environment = await controller.PrepareAsync(workspace, application, CancellationToken.None);
        }
        catch (DllNotFoundException ex)
        {
            Assert.Ignore(ex.Message);
            return;
        }

        application.RuntimeEnvironment = environment;
        var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, application);
        startInfo.Environment["LD_LIBRARY_PATH"] = environment.GetValueOrDefault("LD_LIBRARY_PATH") ?? "";
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
                output.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
                output.AppendLine(args.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (DateTime.UtcNow < deadline && !process.HasExited)
            {
                if (LooksLikeNativeLoadFailure(output.ToString()))
                    break;
                await Task.Delay(100);
            }

            var captured = output.ToString();
            Assert.That(process.HasExited, Is.False, captured);
            Assert.That(LooksLikeNativeLoadFailure(captured), Is.False, captured);
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException ex)
                {
                    Trace.TraceWarning(ex.Message);
                }
            }

            await controller.StopAsync(workspace.Id, application.Name, CancellationToken.None);
        }
    }

    private static void PreloadHostDisplayLibraries(string repositoryRoot)
    {
        var libraryPath = new HostedDesktopNativeLibraryProvider()
            .CreateEnvironment(repositoryRoot)
            .GetValueOrDefault("LD_LIBRARY_PATH");
        if (string.IsNullOrWhiteSpace(libraryPath))
            return;

        foreach (var library in libraryPath
                     .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(Directory.Exists)
                     .SelectMany(directory => new[]
                     {
                         Path.Join(directory, "libX11.so.6"),
                         Path.Join(directory, "libXtst.so.6")
                     })
                     .Where(File.Exists))
        {
            NativeLibrary.TryLoad(library, out _);
        }
    }

    private static string? NoInheritedNativeLibraries(string name) =>
        name is "LD_LIBRARY_PATH" or "NIX_LD_LIBRARY_PATH"
            ? null
            : Environment.GetEnvironmentVariable(name);

    private static bool LooksLikeNativeLoadFailure(string output) =>
        output.Contains("cannot open shared object", StringComparison.OrdinalIgnoreCase)
        || output.Contains("DllNotFoundException", StringComparison.Ordinal)
        || output.Contains("liblibSkiaSharp", StringComparison.Ordinal)
        || output.Contains("Unable to load shared library", StringComparison.OrdinalIgnoreCase);

    private static void EnsureLinuxDesktopBuilt(string repositoryRoot)
    {
        var nativeLibrary = FindLibSkiaSharp(repositoryRoot);
        var runtimeConfig = Path.Join(
            repositoryRoot, "Examples", "linux-desktop", "bin", "Debug", "net10.0", "LinuxDesktop.runtimeconfig.json");
        if (nativeLibrary is not null && File.Exists(runtimeConfig))
            return;

        var workspace = new Workspace
        {
            Id = "hosted-skia-build",
            DisplayName = "hosted-skia-build",
            RepositoryPath = repositoryRoot,
            WorktreePath = repositoryRoot,
            Branch = "main",
            Commit = "test"
        };
        var application = new ApplicationInstance
        {
            Name = "Sample Desktop",
            Command = "dotnet run --project Examples/linux-desktop/LinuxDesktop.csproj --no-launch-profile",
            Install = "dotnet build Examples/linux-desktop/LinuxDesktop.csproj --nologo --no-incremental",
            Path = ".",
            Kind = ApplicationKind.Desktop
        };
        var startInfo = new LocalProcessProvider().CreateInstallStartInfo(workspace, application);
        Assert.That(startInfo, Is.Not.Null);

        using var process = new Process { StartInfo = startInfo! };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.That(process.ExitCode, Is.EqualTo(0), $"{stdout}{stderr}");
    }

    private static string? FindLibSkiaSharp(string repositoryRoot)
    {
        var output = Path.Join(repositoryRoot, "Examples", "linux-desktop", "bin", "Debug", "net10.0");
        var native = Path.Join(output, "runtimes", "linux-x64", "native", "libSkiaSharp.so");
        if (File.Exists(native))
            return native;

        var copied = Path.Join(output, "libSkiaSharp.so");
        return File.Exists(copied) ? copied : null;
    }

    private static IReadOnlyList<string>? TryLddMissing(string nativeLibrary, string libraryPath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ldd",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.StartInfo.ArgumentList.Add(nativeLibrary);
            process.StartInfo.Environment["LD_LIBRARY_PATH"] = libraryPath;
            process.Start();
            var stdout = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Contains("not found", StringComparison.Ordinal))
                .Select(line => line.Trim())
                .ToArray();
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static string? FindRepositoryRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Join(directory, "agent-up.sln"))
                && File.Exists(Path.Join(directory, "Examples", "linux-desktop", "LinuxDesktop.csproj")))
                return directory;

            var parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
                break;
            directory = parent;
        }

        return null;
    }
}
