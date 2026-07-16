using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using System.IO.Compression;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.WindowsPackages.Models;
using AgentUp.Packaging.Features.WindowsPackages.Providers;

namespace AgentUp.Packaging.Features.WindowsPackages.Services;

public sealed class WindowsPackager
{
    private readonly ICommandRunner _commands;
    private readonly IWindowsPackageWriter _writer;
    private readonly PayloadStagingController _payloads;

    public WindowsPackager(ICommandRunner commands, IWindowsPackageWriter writer, PayloadStagingController payloads)
    {
        _commands = commands;
        _writer = writer;
        _payloads = payloads;
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = WindowsPackageLayout.From(request);
        await _payloads.StageAsync(new PayloadStagingRequest(
            request,
            layout.DesktopPublishDirectory,
            layout.ServerPublishDirectory,
            layout.CliPublishDirectory),
            cancellationToken);
        _writer.CreateDirectory(layout.InstallerSourceDirectory);

        var manifest = WindowsPackageManifest.From(request);
        var generator = new WindowsWixSourceGenerator(manifest);
        _writer.WriteText(
            Path.Join(layout.InstallerSourceDirectory, manifest.InstallerManifest.CliShimName),
            WindowsWixSourceGenerator.CliShimText());
        _writer.WriteText(layout.ProductWxsPath, generator.ProductWxs(layout));
        _writer.WriteText(layout.BundleWxsPath, generator.BundleWxs(layout));
        _writer.WriteText(layout.LicenseRtfPath, WindowsWixSourceGenerator.LicenseRtf());

        // WixToolset.Bal.wixext 7.0.0 ships WixToolset.BootstrapperApplications.wixext.dll (renamed DLL);
        // wix build constructs the path using the package name, so it looks for the wrong filename.
        // Download and stage the DLL directly so we can pass an absolute path to wix build.
        var balExtDll = await StageBalExtensionAsync(request.RepositoryRoot, cancellationToken);

        await RunWixAsync(["eula", "accept", "wix7"], cancellationToken);
        await RunWixAsync(
        [
            "build",
            layout.ProductWxsPath,
            "-arch", "x64",
            "-o", layout.ProductMsiPath
        ], cancellationToken);
        await RunWixAsync(
        [
            "build",
            layout.BundleWxsPath,
            "-ext", balExtDll ?? "WixToolset.Bal.wixext",
            "-o", layout.SetupExePath
        ], cancellationToken);
        _writer.CopyFile(layout.ProductMsiPath, layout.ProductMsiOutputPath);
    }

    private Task<CommandResult> RunWixAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var wixCommand = Environment.GetEnvironmentVariable("AGENTUP_WIX_COMMAND") ?? "wix";
        if (OperatingSystem.IsWindows())
            return _commands.RunAsync(new CommandSpec("cmd.exe", ["/c", wixCommand, .. arguments]), cancellationToken);

        return _commands.RunAsync(new CommandSpec(wixCommand, arguments), cancellationToken);
    }

    // WixToolset.Bal.wixext 7.0.0 ships WixToolset.BootstrapperApplications.wixext.dll inside
    // wixext7/, not WixToolset.Bal.wixext.dll. wix build constructs the lookup path from the package
    // name, so it always misses. Download the .nupkg directly and extract the real DLL, then return
    // its absolute path for use with -ext. Returns null on non-Windows (CLI will fall back to the
    // package name, which works on Linux/macOS where wix build uses a different resolution path).
    private static async Task<string?> StageBalExtensionAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return null;

        var dllPath = Path.Join(repositoryRoot, "packaging", "windows", ".wix", "extensions",
            "WixToolset.Bal.wixext", "7.0.0", "wixext7", "WixToolset.BootstrapperApplications.wixext.dll");

        if (!File.Exists(dllPath))
        {
            const string nupkgUrl = "https://api.nuget.org/v3-flatcontainer/wixtoolset.bal.wixext/7.0.0/wixtoolset.bal.wixext.7.0.0.nupkg";
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(nupkgUrl, cancellationToken);
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var entry = zip.Entries.First(e => e.FullName.Equals("wixext7/WixToolset.BootstrapperApplications.wixext.dll", StringComparison.OrdinalIgnoreCase));
            Directory.CreateDirectory(Path.GetDirectoryName(dllPath)!);
            entry.ExtractToFile(dllPath);
        }

        return dllPath;
    }
}
