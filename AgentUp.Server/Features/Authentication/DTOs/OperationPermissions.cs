namespace AgentUp.Server.Features.Authentication.DTOs;

public static class OperationPermissions
{
    public const string ServerRead = "server.read";
    public const string WorkspaceRead = "workspace.read";
    public const string WorkspaceCreate = "workspace.create";
    public const string WorkspaceStart = "workspace.start";
    public const string WorkspaceStop = "workspace.stop";
    public const string WorkspaceDelete = "workspace.delete";
    public const string ApplicationRead = "application.read";
    public const string ApplicationOperate = "application.operate";
    public const string AgentRead = "agent.read";
    public const string AgentPrompt = "agent.prompt";
    public const string AgentPermissionRespond = "agent.permission.respond";
    public const string GitRead = "git.read";
    public const string GitWrite = "git.write";
    public const string DiagnosticsRead = "diagnostics.read";
    public const string BrowserControl = "browser.control";

    public static IReadOnlyList<string> All { get; } =
    [
        ServerRead,
        WorkspaceRead,
        WorkspaceCreate,
        WorkspaceStart,
        WorkspaceStop,
        WorkspaceDelete,
        ApplicationRead,
        ApplicationOperate,
        AgentRead,
        AgentPrompt,
        AgentPermissionRespond,
        GitRead,
        GitWrite,
        DiagnosticsRead,
        BrowserControl
    ];
}
