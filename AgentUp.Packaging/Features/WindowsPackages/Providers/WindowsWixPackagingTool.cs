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
        var balExtension = await StageBalExtensionAsync(request.RepositoryRoot, cancellationToken);
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

    private static async Task<string?> StageBalExtensionAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var dllPath = PackagePathValidator.ResolveRelativeUnderRoot(repositoryRoot, Path.Join("packaging", "windows", ".wix", "extensions",
            "WixToolset.Bal.wixext", "7.0.0", "wixext7", "WixToolset.BootstrapperApplications.wixext.dll"), nameof(repositoryRoot));

        if (!File.Exists(dllPath))
        {
            const string nupkgUrl = "https://api.nuget.org/v3-flatcontainer/wixtoolset.bal.wixext/7.0.0/wixtoolset.bal.wixext.7.0.0.nupkg";
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(nupkgUrl, cancellationToken);
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var entry = zip.Entries.First(e => e.FullName.Equals("wixext7/WixToolset.BootstrapperApplications.wixext.dll", StringComparison.OrdinalIgnoreCase));
            Directory.CreateDirectory(PackagePathValidator.GetParentDirectory(dllPath, nameof(dllPath)));
            entry.ExtractToFile(dllPath);
        }

        return dllPath;
    }
}
