namespace AgentUp.Server.Features.Files.Models;

/// <summary>A path a client sent that does not name somewhere inside the workspace folder.</summary>
public sealed class WorkspaceFilePathException(string message) : Exception(message);
