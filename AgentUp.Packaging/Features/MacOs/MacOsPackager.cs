using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Features.MacOs;

public sealed class MacOsPackager
{
    private readonly ICommandRunner _commands;
    private readonly IMacOsPackageWriter _writer;

    public MacOsPackager(ICommandRunner commands, IMacOsPackageWriter writer)
    {
        _commands = commands;
        _writer = writer;
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = MacOsPackageLayout.From(request);
        var publisher = new PackagePublisher(_commands);

        _writer.ResetDirectory(request.StageDirectory);
        _writer.CreateDirectory(request.OutputRoot);
        if (request.PayloadRoot is null)
        {
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.InstallerApp", "AgentUp.InstallerApp.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.InstallerPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Desktop", "AgentUp.Desktop.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.DesktopPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Server", "AgentUp.Server.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.ServerPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.CLI", "AgentUp.CLI.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.CliPublishDirectory,
                cancellationToken);
        }
        else
        {
            PackagePublisher.CopyPrebuiltPayload(request.InstallerPayloadDirectory!, layout.InstallerPublishDirectory);
            PackagePublisher.CopyPrebuiltPayload(request.DesktopPayloadDirectory!, layout.DesktopPublishDirectory);
            PackagePublisher.CopyPrebuiltPayload(request.ServerPayloadDirectory!, layout.ServerPublishDirectory);
            PackagePublisher.CopyPrebuiltPayload(request.CliPayloadDirectory!, layout.CliPublishDirectory);
        }

        new MacOsPackageStager(_writer).Stage(layout, MacOsPackageManifest.From(request));

        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.installer",
            "--version", request.NormalizedVersion,
            "--root", layout.InstallerComponentRoot,
            "--install-location", "/",
            layout.InstallerPackagePath
        ]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.desktop",
            "--version", request.NormalizedVersion,
            "--root", layout.DesktopComponentRoot,
            "--install-location", "/",
            layout.DesktopPackagePath
        ]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.cli",
            "--version", request.NormalizedVersion,
            "--root", layout.CliComponentRoot,
            "--install-location", "/",
            layout.CliPackagePath
        ]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.server",
            "--version", request.NormalizedVersion,
            "--root", layout.ServerComponentRoot,
            "--scripts", layout.ScriptsDirectory,
            "--install-location", "/",
            layout.ServerPackagePath
        ]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("productbuild",
        [
            "--distribution", layout.DistributionXmlPath,
            "--package-path", layout.ComponentPackageDirectory,
            layout.ProductPackagePath
        ]), cancellationToken);
    }
}
