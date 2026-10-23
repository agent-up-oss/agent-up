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

    public static IReadOnlyList<string> NotEnabled(string capabilityId, string? applicationName = null)
    {
        var cause = string.IsNullOrWhiteSpace(applicationName)
            ? $"Capability '{capabilityId}' is not enabled."
            : $"Application '{applicationName}' needs capability '{capabilityId}', which is not enabled.";
        return [cause, $"Run: {BootstrapCommand}"];
    }

    public static string LaunchFailure(IReadOnlyList<string> messages)
        => string.Join(Environment.NewLine + "       ", messages);
}
