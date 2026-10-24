namespace AgentUp.CLI.Features.Capabilities.Models;

public static class FirstPartyCapabilities
{
    public const string PackScriptRelativePath = "scripts/pack-first-party-capabilities.sh";

    public static readonly string[] Ids =
    [
        "dotnet",
        "docker",
        "codex",
        "cursor",
        "claude",
        "github",
        "forgejo"
    ];
}
