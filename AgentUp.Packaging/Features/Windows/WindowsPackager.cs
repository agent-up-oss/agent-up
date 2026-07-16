using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Features.Windows;

public sealed class WindowsPackager
{
    private readonly ICommandRunner _commands;
    private readonly IWindowsPackageWriter _writer;

    public WindowsPackager(ICommandRunner commands, IWindowsPackageWriter writer)
    {
        _commands = commands;
        _writer = writer;
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = WindowsPackageLayout.From(request);
        var publisher = new PackagePublisher(_commands);

        _writer.ResetDirectory(request.StageDirectory);
        _writer.CreateDirectory(request.OutputRoot);
        _writer.CreateDirectory(layout.InstallerSourceDirectory);

        if (request.PayloadRoot is null)
        {
            await publisher.PublishDotNetProjectAsync(
                Path.Combine(request.RepositoryRoot, "AgentUp.Desktop", "AgentUp.Desktop.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.DesktopPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Combine(request.RepositoryRoot, "AgentUp.Server", "AgentUp.Server.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.ServerPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Combine(request.RepositoryRoot, "AgentUp.CLI", "AgentUp.CLI.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.CliPublishDirectory,
                cancellationToken);
        }
        else
        {
            PackagePublisher.CopyPrebuiltPayload(request.DesktopPayloadDirectory!, layout.DesktopPublishDirectory);
            PackagePublisher.CopyPrebuiltPayload(request.ServerPayloadDirectory!, layout.ServerPublishDirectory);
            PackagePublisher.CopyPrebuiltPayload(request.CliPayloadDirectory!, layout.CliPublishDirectory);
        }

        var manifest = WindowsPackageManifest.From(request);
        var generator = new WindowsWixSourceGenerator(manifest);
        _writer.WriteText(
            Path.Combine(layout.InstallerSourceDirectory, manifest.InstallerManifest.CliShimName),
            WindowsWixSourceGenerator.CliShimText());
        _writer.WriteText(layout.ProductWxsPath, generator.ProductWxs(layout));
        _writer.WriteText(layout.BundleWxsPath, generator.BundleWxs(layout));
        _writer.WriteText(layout.LicenseRtfPath, WindowsWixSourceGenerator.LicenseRtf());

        await RunWixAsync(["eula", "accept", "wix7"], cancellationToken);
        await RunWixAsync(["extension", "add", "WixToolset.Bal.wixext/7.0.0"], cancellationToken);
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
            "-ext", "WixToolset.Bal.wixext",
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
}
