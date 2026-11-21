using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Services;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;
using AgentUp.Capabilities.Github.Features.GithubCapability.Services;
using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Services;
using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Capabilities.Providers;

/// <summary>
/// The capabilities this Server is built with. They are always installed; enabling one only
/// decides whether the Server offers its feature.
/// </summary>
public sealed class FirstPartyCapabilityCatalog : ICapabilityCatalog
{
    public IReadOnlyList<ICapability> Modules { get; } =
    [
        new DotnetRuntimeCapability(),
        new DockerRuntimeCapability(),
        new CodexAgentCapability(),
        new CursorAgentCapability(),
        new ClaudeAgentCapability(),
        new OpencodeAgentCapability(),
        new GithubForgeCapability(),
        new ForgejoForgeCapability()
    ];
}
