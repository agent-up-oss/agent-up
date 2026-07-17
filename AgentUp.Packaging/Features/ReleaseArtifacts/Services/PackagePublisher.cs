using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Shared.Providers;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Services;

public sealed class PackagePublisher
{
    private readonly ICommandRunner _commands;

    public PackagePublisher(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task PublishDotNetProjectAsync(
        string projectPath,
        string runtimeId,
        string configuration,
        string version,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        await _commands.RunAsync(new CommandSpec("dotnet", ["restore", projectPath, "--runtime", runtimeId]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("dotnet",
        [
            "publish",
            projectPath,
            "--configuration", configuration,
            "--runtime", runtimeId,
            "--self-contained", "true",
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:IncludeAllContentForSelfExtract=true",
            "-p:DebugType=none",
            "-p:DebugSymbols=false",
            $"-p:Version={version}",
            "-o", outputDirectory
        ]), cancellationToken);
    }

    public void CopyPrebuiltPayload(string payloadDirectory, string outputDirectory)
    {
        var fullPayloadDirectory = PackagePathValidator.RequireFullyQualifiedPath(payloadDirectory, nameof(payloadDirectory));
        var fullOutputDirectory = PackagePathValidator.RequireFullyQualifiedPath(outputDirectory, nameof(outputDirectory));
        if (!Directory.Exists(fullPayloadDirectory))
            throw new DirectoryNotFoundException($"Prebuilt payload directory does not exist: {fullPayloadDirectory}");

        if (Directory.Exists(fullOutputDirectory))
            Directory.Delete(fullOutputDirectory, recursive: true);

        FileSystemDirectoryCopier.Copy(fullPayloadDirectory, fullOutputDirectory);
    }
}
