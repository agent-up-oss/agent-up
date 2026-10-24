using AgentUp.Desktop.Features.Validation.Models;

namespace AgentUp.Desktop.Features.Validation.Providers;

public sealed class DesktopValidationEnvironment
{
    public bool SkipCosmeticStaging =>
        string.Equals(Read(DesktopValidationKeys.SkipCosmetic), "1", StringComparison.Ordinal);

    public DesktopValidationLaunchRequest? ReadLaunch()
    {
        var surface = Read(DesktopValidationKeys.Surface);
        if (!string.Equals(surface, DesktopValidationKeys.DesktopValue, StringComparison.OrdinalIgnoreCase))
            return null;

        var output = Read(DesktopValidationKeys.Output);
        if (string.IsNullOrWhiteSpace(output))
            return null;

        return new DesktopValidationLaunchRequest(
            output,
            EmptyToNull(Read(DesktopValidationKeys.Flow)),
            EmptyToNull(Read(DesktopValidationKeys.Workspace)),
            SkipCosmeticStaging);
    }

    public string? ServerLogPath => EmptyToNull(Read(DesktopValidationKeys.ServerLog));

    public string? AdminPassword => EmptyToNull(Environment.GetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD"));

    private static string? Read(string key) => Environment.GetEnvironmentVariable(key);

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
