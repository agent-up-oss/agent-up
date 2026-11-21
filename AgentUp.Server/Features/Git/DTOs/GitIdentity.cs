namespace AgentUp.Server.Features.Git.DTOs;

/// <summary>Who a commit the Server makes on a team member's behalf is attributed to.</summary>
public sealed record GitIdentity(string Name, string Email);
