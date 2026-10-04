using AgentUp.Server.Features.Authentication.DTOs;

namespace AgentUp.Server.Features.Authentication.Providers;

/// <summary>
/// The MCP half of <see cref="OperationPermissionMap"/>. REST actions are keyed by controller and
/// action; MCP tools are keyed by the protocol name a caller sends, which is the only identity a
/// JSON-RPC request carries. Every declared tool must appear here: an unmapped tool is treated as
/// forbidden for a permission-scoped caller, and AgentUp.Architecture.Tests fails on the gap.
/// </summary>
public static class McpToolPermissionMap
{
    public static string? For(string tool)
        => Required.GetValueOrDefault(tool);

    public static IReadOnlyDictionary<string, string> Required { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Reading the queue is reading the repository; every mutation writes an Agent-Up ref.
        ["get_commits_status"] = OperationPermissions.GitRead,
        ["guard_commits"] = OperationPermissions.GitRead,
        ["get_commit_changes"] = OperationPermissions.GitRead,
        ["inspect_commit"] = OperationPermissions.GitRead,
        ["enqueue_commit"] = OperationPermissions.GitWrite,
        ["enqueue_review_fix_commit"] = OperationPermissions.GitWrite,
        ["update_commit_message"] = OperationPermissions.GitWrite,
        ["add_commit_files"] = OperationPermissions.GitWrite,
        ["remove_commit_files"] = OperationPermissions.GitWrite,
        ["remove_commit"] = OperationPermissions.GitWrite,
        ["restore_commit"] = OperationPermissions.GitWrite,
        ["clear_commits"] = OperationPermissions.GitWrite,
        ["begin_commit_edit"] = OperationPermissions.GitWrite,
        ["save_commit_edit"] = OperationPermissions.GitWrite,
        ["abort_commit_edit"] = OperationPermissions.GitWrite,

        // Planning and the guard only hash files and read the receipt ledger. Running a check
        // executes a repository command and writes a receipt.
        ["plan_verification"] = OperationPermissions.GitRead,
        ["guard_verification"] = OperationPermissions.GitRead,
        ["run_verification"] = OperationPermissions.GitWrite,
        ["run_verification_check"] = OperationPermissions.GitWrite,

        ["list_workspaces"] = OperationPermissions.WorkspaceRead,
        ["get_workspace_status"] = OperationPermissions.WorkspaceRead,
        ["get_workspace_console"] = OperationPermissions.WorkspaceRead,
        ["get_agent_up_context"] = OperationPermissions.WorkspaceRead,
        ["get_agent_up_json_format"] = OperationPermissions.WorkspaceRead,
        ["start_workspace"] = OperationPermissions.WorkspaceStart,
        ["stop_workspace"] = OperationPermissions.WorkspaceStop,
        ["get_workspace_diagnostics"] = OperationPermissions.DiagnosticsRead,

        ["browser_navigate"] = OperationPermissions.BrowserControl,
        ["browser_inspect"] = OperationPermissions.BrowserControl,
        ["browser_click"] = OperationPermissions.BrowserControl,
        ["browser_fill"] = OperationPermissions.BrowserControl,
        ["browser_press"] = OperationPermissions.BrowserControl,
        ["browser_wait_for_selector"] = OperationPermissions.BrowserControl,
        ["browser_wait_for_text"] = OperationPermissions.BrowserControl,
        ["browser_wait_for_navigation"] = OperationPermissions.BrowserControl,
        ["browser_screenshot"] = OperationPermissions.BrowserControl,

        // A hosted desktop application is an application, not a browser: looking at its
        // framebuffer reads it, and sending input operates it. This matches the REST rows for
        // DesktopApplicationsHttp.
        ["desktop_inspect"] = OperationPermissions.ApplicationRead,
        ["desktop_screenshot"] = OperationPermissions.ApplicationRead,
        ["desktop_click"] = OperationPermissions.ApplicationOperate,
        ["desktop_fill"] = OperationPermissions.ApplicationOperate,
        ["desktop_press"] = OperationPermissions.ApplicationOperate,

        ["list_validation_flows"] = OperationPermissions.ApplicationRead,
        ["export_validation_flow"] = OperationPermissions.ApplicationRead,
        ["save_validation_flow"] = OperationPermissions.ApplicationOperate,
        ["play_validation_flow"] = OperationPermissions.ApplicationOperate,
        ["delete_validation_flow"] = OperationPermissions.ApplicationOperate,

        ["audit_query"] = OperationPermissions.DiagnosticsRead,
        ["audit_timeline"] = OperationPermissions.DiagnosticsRead,
        ["audit_get_event"] = OperationPermissions.DiagnosticsRead,
        ["audit_load_artifact"] = OperationPermissions.DiagnosticsRead,

        // Enabling a module changes what this Server will host, so it sits with the other
        // server-administration actions rather than with any one workspace's permissions.
        ["list_capability_modules"] = OperationPermissions.ServerRead,
        ["enable_capability_module"] = OperationPermissions.ServerRead,
        ["disable_capability_module"] = OperationPermissions.ServerRead
    };
}
