using AgentUp.Server.Features.Authentication.DTOs;

namespace AgentUp.Server.Features.Authentication.Providers;

public static class OperationPermissionMap
{
    public static string? For(string controller, string action)
        => Required.GetValueOrDefault((controller, action));

    public static bool IsOpenAuthenticated(string controller, string action)
        => OpenAuthenticated.Contains((controller, action));

    private static readonly HashSet<(string Controller, string Action)> OpenAuthenticated =
    [
        ("Entitlements", "Get")
    ];

    private static readonly Dictionary<(string Controller, string Action), string> Required = new()
    {
        [("Workspaces", "SubscribeEvents")] = OperationPermissions.WorkspaceRead,
        [("Workspaces", "GetAll")] = OperationPermissions.WorkspaceRead,
        [("Workspaces", "GetById")] = OperationPermissions.WorkspaceRead,
        [("Workspaces", "GetOverview")] = OperationPermissions.WorkspaceRead,
        [("Workspaces", "Register")] = OperationPermissions.WorkspaceCreate,
        [("Workspaces", "Start")] = OperationPermissions.WorkspaceStart,
        [("Workspaces", "Stop")] = OperationPermissions.WorkspaceStop,
        [("Workspaces", "CleanupTutorialWorkspaces")] = OperationPermissions.WorkspaceDelete,
        [("Workspaces", "UpdateState")] = OperationPermissions.WorkspaceStart,
        [("Workspaces", "Delete")] = OperationPermissions.WorkspaceDelete,

        [("Applications", "GetApplications")] = OperationPermissions.ApplicationRead,
        [("Applications", "StartApplication")] = OperationPermissions.ApplicationOperate,
        [("Applications", "StopApplication")] = OperationPermissions.ApplicationOperate,
        [("Applications", "RestartApplication")] = OperationPermissions.ApplicationOperate,
        [("Applications", "GetOutput")] = OperationPermissions.ApplicationRead,
        [("ApplicationMetricsHttp", "GetMetrics")] = OperationPermissions.ApplicationRead,
        [("DatabaseHttp", "ListDatabases")] = OperationPermissions.ApplicationRead,
        [("DatabaseHttp", "ListTables")] = OperationPermissions.ApplicationRead,
        [("DatabaseHttp", "ExecuteQuery")] = OperationPermissions.ApplicationOperate,

        [("AgentsHttp", "Get")] = OperationPermissions.AgentRead,
        [("AgentsHttp", "Schedule")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Resume")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Prompt")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Authenticate")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "SubmitLoginCode")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "SubmitLoginCallback")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Cancel")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Decide")] = OperationPermissions.AgentPermissionRespond,
        [("AgentsHttp", "Stop")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Events")] = OperationPermissions.AgentRead,

        [("GitChanges", "GetChanges")] = OperationPermissions.GitRead,
        [("GitChanges", "GetHead")] = OperationPermissions.GitRead,
        [("GitChanges", "GetFileDiff")] = OperationPermissions.GitRead,
        [("GitChanges", "GetLog")] = OperationPermissions.GitRead,
        [("GitChanges", "Commit")] = OperationPermissions.GitWrite,
        [("GitChanges", "Discard")] = OperationPermissions.GitWrite,
        [("GitChanges", "SwitchBranch")] = OperationPermissions.GitWrite,
        [("GitChanges", "Checkout")] = OperationPermissions.GitWrite,
        [("GitChanges", "Fetch")] = OperationPermissions.GitWrite,
        [("GitChanges", "Pull")] = OperationPermissions.GitWrite,
        [("GitChanges", "Push")] = OperationPermissions.GitWrite,
        [("WorkspaceCommitQueue", "Get")] = OperationPermissions.GitRead,

        [("WorkspaceDiagnosticsHttp", "Get")] = OperationPermissions.DiagnosticsRead,
        [("AuditHttp", "Record")] = OperationPermissions.DiagnosticsRead,
        [("AuditHttp", "QueryApplication")] = OperationPermissions.DiagnosticsRead,
        [("ApplicationAuditStreamHttp", "Stream")] = OperationPermissions.DiagnosticsRead,

        [("BrowserRemoteDisplay", "StreamAsync")] = OperationPermissions.BrowserControl,
        [("BrowserRemoteDisplay", "LatestFrame")] = OperationPermissions.BrowserControl,
        [("BrowserRemoteDisplay", "ChromiumStatus")] = OperationPermissions.BrowserControl,
        [("BrowserSession", "GetCurrentUrl")] = OperationPermissions.BrowserControl,
        [("BrowserSession", "Navigate")] = OperationPermissions.BrowserControl,
        [("BrowserSession", "NavigateBack")] = OperationPermissions.BrowserControl,
        [("BrowserSession", "NavigateForward")] = OperationPermissions.BrowserControl,
        [("BrowserSession", "Reload")] = OperationPermissions.BrowserControl,
        [("BrowserInput", "GetControlMode")] = OperationPermissions.BrowserControl,
        [("BrowserInput", "SetControlModeAsync")] = OperationPermissions.BrowserControl,
        [("BrowserInput", "SetViewportAsync")] = OperationPermissions.BrowserControl,
        [("BrowserEventStream", "StreamAsync")] = OperationPermissions.BrowserControl,
        [("BrowserRemoteSession", "Get")] = OperationPermissions.BrowserControl,
        [("BrowserViewer", "Mode")] = OperationPermissions.BrowserControl,

        [("DesktopApplicationsHttp", "Get")] = OperationPermissions.ApplicationRead,
        [("DesktopApplicationsHttp", "CreateViewerTicket")] = OperationPermissions.ApplicationOperate,
        [("DesktopApplicationsHttp", "Screenshot")] = OperationPermissions.ApplicationRead,
        [("DesktopApplicationsHttp", "PointerDown")] = OperationPermissions.ApplicationOperate,
        [("DesktopApplicationsHttp", "PointerUp")] = OperationPermissions.ApplicationOperate,
        [("DesktopApplicationsHttp", "Key")] = OperationPermissions.ApplicationOperate,

        [("SourceClones", "GetRoot")] = OperationPermissions.WorkspaceRead,
        [("SourceClones", "Clone")] = OperationPermissions.WorkspaceCreate,
        [("ValidationFlows", "List")] = OperationPermissions.ApplicationRead,
        [("ValidationFlows", "Get")] = OperationPermissions.ApplicationRead,
        [("ValidationFlows", "Export")] = OperationPermissions.ApplicationRead,
        [("ValidationFlows", "Save")] = OperationPermissions.ApplicationOperate,
        [("ValidationFlows", "Delete")] = OperationPermissions.ApplicationOperate,
        [("ValidationFlows", "Run")] = OperationPermissions.ApplicationOperate,
        [("ApplicationProxyTickets", "Issue")] = OperationPermissions.ApplicationOperate,

        [("TraySession", "PostHeartbeat")] = OperationPermissions.ServerRead,
        [("ServiceControl", "Restart")] = OperationPermissions.ServerRead,
        [("ServiceControl", "Shutdown")] = OperationPermissions.ServerRead
    };
}
