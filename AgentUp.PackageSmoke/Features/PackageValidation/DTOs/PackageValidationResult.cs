using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using System.Text;

namespace AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

public enum FindingSeverity
{
    Info,
    Error
}

public sealed record SmokeFinding(FindingSeverity Severity, string Code, string Message);

public sealed record PackageValidationResult(
    string? ServerPath,
    string? CliPath,
    IReadOnlyList<SmokeFinding> Findings)
{
    public bool Succeeded => Findings.All(finding => finding.Severity != FindingSeverity.Error);

    public string ToEnvironmentFile()
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(ServerPath))
            builder.AppendLine($"SERVER_PATH={ShellQuote(ServerPath)}");
        if (!string.IsNullOrWhiteSpace(CliPath))
            builder.AppendLine($"CLI_PATH={ShellQuote(CliPath)}");
        return builder.ToString();
    }

    private static string ShellQuote(string value)
        => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
}
