namespace AgentUp.Packaging.Features.ReleaseArtifacts;

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
            "-p:PublishSingleFile=false",
            $"-p:Version={version}",
            "-o", outputDirectory
        ]), cancellationToken);
    }
}
