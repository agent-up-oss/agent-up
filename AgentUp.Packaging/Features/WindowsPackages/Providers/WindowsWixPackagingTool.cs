using System.IO.Compression;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.WindowsPackages.Models;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Shared.Providers;

namespace AgentUp.Packaging.Features.WindowsPackages.Providers;

public sealed class WindowsWixPackagingTool : IWindowsPackagingTool
{
    private readonly ICommandRunner _commands;

    public WindowsWixPackagingTool(ICommandRunner commands)
    {
        _commands = commands;
    }

    public Task AcceptWixLicenseAsync(CancellationToken cancellationToken = default)
        => RunWixAsync(["eula", "accept", "wix7"], cancellationToken);

    public Task BuildProductMsiAsync(WindowsPackageLayout layout, CancellationToken cancellationToken = default)
        => RunWixAsync(
        [
            "build",
            layout.ProductWxsPath,
            "-arch", "x64",
            "-o", layout.ProductMsiPath
        ], cancellationToken);

    public async Task BuildBundleAsync(PackageRequest request, WindowsPackageLayout layout, CancellationToken cancellationToken = default)
    {
        var balExtension = await StageWixExtensionAsync(
            request.RepositoryRoot,
            "WixToolset.Bal.wixext",
            "WixToolset.BootstrapperApplications.wixext.dll",
            cancellationToken);
        await RunWixAsync(
        [
            "build",
            layout.BundleWxsPath,
            "-ext", balExtension ?? "WixToolset.Bal.wixext",
            "-o", layout.SetupExePath
        ], cancellationToken);
    }

    private Task<CommandResult> RunWixAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var wixCommand = Environment.GetEnvironmentVariable("AGENTUP_WIX_COMMAND") ?? "wix";
        if (OperatingSystem.IsWindows())
            return _commands.RunAsync(new CommandSpec("cmd.exe", ["/c", wixCommand, .. arguments]), cancellationToken);

        return _commands.RunAsync(new CommandSpec(wixCommand, arguments), cancellationToken);
    }

    private static async Task<string?> StageWixExtensionAsync(
        string repositoryRoot,
        string packageId,
        string extensionFileName,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        const string version = "7.0.0";
        var normalizedPackage = packageId.ToLowerInvariant();
        var dllPath = PackagePathValidator.ResolveRelativeUnderRoot(repositoryRoot, Path.Join("packaging", "windows", ".wix", "extensions",
            packageId, version, "wixext7", extensionFileName), nameof(repositoryRoot));

        if (!File.Exists(dllPath))
        {
            var nupkgUrl = $"https://api.nuget.org/v3-flatcontainer/{normalizedPackage}/{version}/{normalizedPackage}.{version}.nupkg";
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(nupkgUrl, cancellationToken);
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var entry = zip.Entries.First(e => e.FullName.Equals($"wixext7/{extensionFileName}", StringComparison.OrdinalIgnoreCase));
            Directory.CreateDirectory(PackagePathValidator.GetParentDirectory(dllPath, nameof(dllPath)));
            entry.ExtractToFile(dllPath);
        }

        return dllPath;
    }
}
