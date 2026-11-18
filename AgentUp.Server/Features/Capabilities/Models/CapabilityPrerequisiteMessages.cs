namespace AgentUp.Server.Features.Capabilities.Models;

public static class CapabilityPrerequisiteMessages
{
    public const string NixInstallUrl = "https://nixos.org/download";
    public const string BootstrapCommand = "agent-up capabilities bootstrap";

    public static IReadOnlyList<string> NixMissing(string capabilityId)
    {
        var provisioned = capabilityId switch
        {
            "dotnet" => "the .NET SDK",
            "docker" => "Docker",
            "opencode" => "OpenCode",
            "codex" or "cursor" or "claude" => "the agent CLI",
            _ => "its toolchain"
        };

        return
        [
            $"Capability '{capabilityId}' needs Nix to provide {provisioned}, and Nix was not found.",
            $"Install Nix: {NixInstallUrl}",
            "On Windows, run the Server under WSL2."
        ];
    }

    /// <summary>Why a capability is unavailable: Nix first, because it is the larger install.</summary>
    public static IReadOnlyList<string> SoftwareMissing(string capabilityId, IReadOnlyList<string> missing)
    {
        var executables = missing.Where(name => name != "nix").ToArray();
        var messages = missing.Contains("nix") ? NixMissing(capabilityId).ToList() : [];
        if (executables.Length > 0)
        {
            messages.Add($"Capability '{capabilityId}' needs {string.Join(", ", executables)}, "
                         + "which was not found on the Server's search path.");
        }

        return messages;
    }

    public static IReadOnlyList<string> NotEnabled(string capabilityId, string? applicationName = null)
    {
        var cause = string.IsNullOrWhiteSpace(applicationName)
            ? $"Capability '{capabilityId}' is not enabled."
            : $"Application '{applicationName}' needs capability '{capabilityId}', which is not enabled.";
        return [cause, $"Run: {BootstrapCommand}"];
    }

    public static IReadOnlyList<string> SettingsMissing(string displayName, IReadOnlyList<string> labels)
        => [$"Set {string.Join(" and ", labels)} for {displayName} in Settings."];

    public static string LaunchFailure(IReadOnlyList<string> messages)
        => string.Join(Environment.NewLine + "       ", messages);
}
