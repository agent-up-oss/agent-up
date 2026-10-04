using Microsoft.AspNetCore.Authorization;

namespace AgentUp.Server.Features.Authentication.Models;

/// <summary>
/// Satisfied by a loopback caller, or by a caller presenting the named operation permission.
/// Loopback MCP keeps its anonymous contract even when remote MCP access is enabled, because
/// on loopback the network restriction is the credential.
/// </summary>
public sealed record McpLoopbackOrPermissionRequirement(string Permission) : IAuthorizationRequirement;
