namespace AgentUp.Server.Features.Forge.Models;

public sealed record ForgeOAuthConfiguration(string ClientId, string ClientSecret, string RedirectUri)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);

    /// <summary>
    /// A callback on the same Server as <see cref="RedirectUri"/>. A second sign-in on this Server
    /// returns to its own path, because the host sends the state back to whichever path it was
    /// given and only the flow that issued a state can spend it.
    /// </summary>
    public string RedirectTo(string path) =>
        Uri.TryCreate(RedirectUri, UriKind.Absolute, out var redirect)
            ? new Uri(redirect, path).AbsoluteUri
            : RedirectUri;
}
