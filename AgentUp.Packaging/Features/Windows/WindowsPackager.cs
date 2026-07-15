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

        var manifest = WindowsInstallerManifest.From(request);
        var generator = new WindowsWixSourceGenerator(manifest);
        _writer.WriteText(Path.Combine(layout.InstallerSourceDirectory, manifest.CliShimName),
            "@echo off\r\n\"%~dp0..\\cli\\AgentUp.CLI.exe\" %*\r\n");
        _writer.WriteText(layout.ProductWxsPath, generator.ProductWxs(layout));
        _writer.WriteText(layout.BundleWxsPath, generator.BundleWxs(layout));
        _writer.WriteText(layout.LicenseRtfPath, WindowsWixSourceGenerator.LicenseRtf());

        await _commands.RunAsync(new CommandSpec("wix",
        [
            "build",
            layout.ProductWxsPath,
            "-arch", "x64",
            "-o", layout.ProductMsiPath
        ]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("wix",
        [
            "build",
            layout.BundleWxsPath,
            "-ext", "WixToolset.Bal.wixext",
            "-o", layout.SetupExePath
        ]), cancellationToken);
    }
}
