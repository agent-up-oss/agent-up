using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

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

    public static void CopyPrebuiltPayload(string payloadDirectory, string outputDirectory)
    {
        if (!Directory.Exists(payloadDirectory))
            throw new DirectoryNotFoundException($"Prebuilt payload directory does not exist: {payloadDirectory}");

        if (Directory.Exists(outputDirectory))
            Directory.Delete(outputDirectory, recursive: true);

        FileSystemDirectoryCopier.Copy(payloadDirectory, outputDirectory);
    }
}
