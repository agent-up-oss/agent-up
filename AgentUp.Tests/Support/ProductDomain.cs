using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Tests.Support;

/// <summary>
/// The shared domain vocabulary for the cross-product end-to-end tests: one workspace and
/// the application it serves, named once and reused by both the Server-side registration
/// and the Desktop-side stub that stands in for it.
/// </summary>
/// <remarks>
/// These tests drive the Server and the Desktop against each other, so the same workspace
/// is described twice in two projects' DTOs. Naming it here is what keeps those two
/// descriptions the same workspace rather than two that happen to look alike.
/// </remarks>
internal static class ProductDomain
{
    public const string Branch = "main";
    public const string Commit = "e2ec0de";
    public const string RunningState = "Running";
    public const string PortVariable = "PORT";

    /// <summary>The workspace an end-to-end run registers with the Server.</summary>
    public static RegisterWorkspaceRequestBuilder Workspace() => new();

    /// <summary>The same workspace as the Desktop reads it back from the Server.</summary>
    public static WorkspaceDtoBuilder KnownWorkspace(string id)
        => new WorkspaceDtoBuilder().Identified(id);
}
