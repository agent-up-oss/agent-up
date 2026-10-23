namespace AgentUp.Server.Features.Forge.Models;

/// <summary>
/// Host-neutral copy for Git hosting availability. Product names belong in forge modules.
/// </summary>
public static class ForgeHostingCopy
{
    public const string NotInstalled = "notInstalled";
    public const string NeedsCredential = "needsCredential";
    public const string NoRemote = "noRemote";
    public const string Unreachable = "unreachable";
    public const string NoProposal = "noProposal";
    public const string Ready = "ready";

    public const string NotInstalledDetail = "No forge capability package is enabled.";
    public const string NotInstalledRemedy = "Run: agent-up capabilities bootstrap";
    public const string NeedsCredentialDetail = "This workspace has no repository host credential.";
    public const string NeedsCredentialRemedy = "Sign in to your repository host.";
    public const string OAuthMissingDetail = "Repository host OAuth is not configured.";
    public const string OAuthMissingRemedy =
        "Set AGENTUP_FORGE_CLIENT_ID, AGENTUP_FORGE_CLIENT_SECRET, and AGENTUP_FORGE_REDIRECT_URI.";
    public const string NoRemoteDetail = "No repository host origin is configured.";
    public const string NoRemoteRemedy = "Add a git remote for this worktree.";
    public const string UnreachableDetail = "Could not reach the host.";
    public const string UnreachableRemedy = "Check the repository host URL and retry.";
    public const string NoProposalDetail = "No open proposal on this branch.";
    public const string OAuthUnavailableTitle = "OAuth unavailable";
    public const string MissingForgeDetail = "No forge capability package is enabled.";
    public const string MissingToken = "The host did not return an access token.";
    public const string EmptyToken = "The host returned an empty access token.";
}
