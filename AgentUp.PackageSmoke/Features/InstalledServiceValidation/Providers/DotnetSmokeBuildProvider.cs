using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;

public sealed class DotnetSmokeBuildProvider(ICommandRunner commands)
{
    public async Task<IReadOnlyList<SmokeFinding>> BuildAsync(string repo, CancellationToken cancellationToken = default)
    {
        var findings = new List<SmokeFinding>();
        foreach (var command in Commands(repo))
        {
            var result = await commands.RunAsync(command.Spec, cancellationToken);
            if (result.ExitCode != 0)
                findings.Add(new SmokeFinding(FindingSeverity.Error, command.Code,
                    $"{command.Spec.FileName} failed: {result.Stderr}{result.Stdout}"));
        }

        return findings;
    }

    private static IReadOnlyList<(CommandSpec Spec, string Code)> Commands(string repo)
    {
        var project = Path.Join(repo, "SmokeDotnet", "SmokeDotnet.csproj");
        return
        [
            (new CommandSpec("dotnet", ["restore", project]), "capability.smokedotnet.restore"),
            (new CommandSpec("dotnet", ["build", project, "--no-restore"]), "capability.smokedotnet.build")
        ];
    }
}
