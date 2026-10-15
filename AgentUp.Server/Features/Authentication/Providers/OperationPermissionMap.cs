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
        [("AgentsHttp", "Bind")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Unbind")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "GetPolicy")] = OperationPermissions.AgentRead,
        [("AgentsHttp", "PutPolicy")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "GetRole")] = OperationPermissions.AgentRead,
        [("AgentsHttp", "PutRole")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "DeleteRole")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "PutHostConnection")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "PutQueueOrder")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "PutAutonomy")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "GetLoop")] = OperationPermissions.AgentRead,
        [("AgentsHttp", "Resume")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Prompt")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Authenticate")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "SubmitLoginCode")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "SubmitLoginCallback")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Cancel")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Decide")] = OperationPermissions.AgentPermissionRespond,
        [("AgentsHttp", "Stop")] = OperationPermissions.AgentPrompt,
        [("AgentsHttp", "Events")] = OperationPermissions.AgentRead,

        [("AutonomyHttp", "Get")] = OperationPermissions.ServerRead,
        [("AutonomyHttp", "Put")] = OperationPermissions.ServerRead,

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
        [("VerificationHttp", "Export")] = OperationPermissions.GitRead,
        [("ForgeHttp", "Get")] = OperationPermissions.GitRead,
        [("ForgeHttp", "Login")] = OperationPermissions.GitWrite,
        [("ForgeHttp", "Logout")] = OperationPermissions.GitWrite,
        [("ForgeHttp", "OpenProposal")] = OperationPermissions.GitWrite,
        [("ForgeHttp", "UpdateRef")] = OperationPermissions.GitWrite,
        [("ForgeHttp", "Comment")] = OperationPermissions.GitWrite,
        [("ForgeHttp", "SubmitReview")] = OperationPermissions.GitWrite,

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
        [("SourceClones", "Refresh")] = OperationPermissions.WorkspaceCreate,
        [("ValidationFlows", "List")] = OperationPermissions.ApplicationRead,
        [("ValidationFlows", "Get")] = OperationPermissions.ApplicationRead,
        [("ValidationFlows", "Export")] = OperationPermissions.ApplicationRead,
        [("ValidationFlows", "Save")] = OperationPermissions.ApplicationOperate,
        [("ValidationFlows", "Delete")] = OperationPermissions.ApplicationOperate,
        [("ValidationFlows", "Run")] = OperationPermissions.ApplicationOperate,
        [("ApplicationProxyTickets", "Issue")] = OperationPermissions.ApplicationOperate,

        [("TraySession", "PostHeartbeat")] = OperationPermissions.ServerRead,
        [("ServiceControl", "Restart")] = OperationPermissions.ServerRead,
        [("ServiceControl", "Shutdown")] = OperationPermissions.ServerRead,

        // Enabling a capability module changes what this Server will host, so it sits with the
        // other server-administration actions rather than with any one workspace's permissions.
        [("CapabilityModulesHttp", "List")] = OperationPermissions.ServerRead,
        [("CapabilityModulesHttp", "Enable")] = OperationPermissions.ServerRead,
        [("CapabilityModulesHttp", "Disable")] = OperationPermissions.ServerRead
    };
}
