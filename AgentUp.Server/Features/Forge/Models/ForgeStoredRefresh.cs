namespace AgentUp.Server.Features.Forge.Models;

/// <summary>The part of an OAuth grant kept beside the access token: how to renew it, and when.</summary>
public sealed record ForgeStoredRefresh(string RefreshToken, DateTimeOffset? ExpiresAt);
