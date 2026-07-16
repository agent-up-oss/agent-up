namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public sealed class PackagePayloadStager
{
    private readonly ICommandRunner _commands;
    private readonly IPackageFileSystem _files;

    public PackagePayloadStager(ICommandRunner commands, IPackageFileSystem files)
    {
        _commands = commands;
        _files = files;
    }

    public async Task StageAsync(
        PackageRequest request,
        string? installerPublishDirectory,
        string desktopPublishDirectory,
        string serverPublishDirectory,
        string cliPublishDirectory,
        CancellationToken cancellationToken = default)
    {
        var publisher = new PackagePublisher(_commands);

        _files.ResetDirectory(request.StageDirectory);
        _files.CreateDirectory(request.OutputRoot);

        if (request.PayloadRoot is null)
        {
            if (installerPublishDirectory is not null)
            {
                await publisher.PublishDotNetProjectAsync(
                    Path.Join(request.RepositoryRoot, "AgentUp.InstallerApp", "AgentUp.InstallerApp.csproj"),
                    request.RuntimeId,
                    request.Configuration,
                    request.Version,
                    installerPublishDirectory,
                    cancellationToken);
            }

            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Desktop", "AgentUp.Desktop.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                desktopPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Server", "AgentUp.Server.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                serverPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.CLI", "AgentUp.CLI.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                cliPublishDirectory,
                cancellationToken);
            return;
        }

        if (installerPublishDirectory is not null)
            PackagePublisher.CopyPrebuiltPayload(request.InstallerPayloadDirectory!, installerPublishDirectory);

        PackagePublisher.CopyPrebuiltPayload(request.DesktopPayloadDirectory!, desktopPublishDirectory);
        PackagePublisher.CopyPrebuiltPayload(request.ServerPayloadDirectory!, serverPublishDirectory);
        PackagePublisher.CopyPrebuiltPayload(request.CliPayloadDirectory!, cliPublishDirectory);
    }
}
