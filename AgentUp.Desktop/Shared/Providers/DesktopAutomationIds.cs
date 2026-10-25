namespace AgentUp.Desktop.Shared.Providers;

public static class DesktopAutomationIds
{
    public const string WindowMinimize = "window.minimize";
    public const string WindowRestore = "window.restore";
    public const string WindowClose = "window.close";
    public const string ChromeReload = "chrome.reload";
    public const string ChromeCapabilities = "chrome.capabilities";
    public const string ChromeServerStatus = "chrome.server-status";

    public const string LoginBack = "login.back";
    public const string LoginServerUrl = "login.server-url";
    public const string LoginSavedList = "login.saved-list";
    public const string LoginPassword = "login.password";
    public const string LoginConnect = "login.connect";
    public const string LoginSignIn = "login.sign-in";

    public const string SidebarAddWorkspace = "sidebar.add-workspace";
    public const string SidebarAddWorkspaceCollapsed = "sidebar.add-workspace.collapsed";
    public const string SidebarToggle = "sidebar.toggle";
    public const string SidebarToggleCollapsed = "sidebar.toggle.collapsed";
    public const string SidebarRetry = "sidebar.retry";
    public const string WorkspaceList = "workspace.list";
    public const string WorkspaceListCollapsed = "workspace.list.collapsed";

    public const string ShellTabs = "shell.tabs";
    public const string ShellTabOverview = "shell.tab.overview";
    public const string OverviewExportReport = "overview.export-report";
    public const string ShellTabAgent = "shell.tab.agent";
    public const string ShellTabGit = "shell.tab.git";
    public const string ApplicationTabs = "app.tabs";
    public const string SubNav = "app.subnav";

    public const string BrowserBack = "browser.back";
    public const string BrowserForward = "browser.forward";
    public const string BrowserReload = "browser.reload";
    public const string BrowserAddress = "browser.address";

    public const string AgentLoop = "agent.loop";
    public const string AgentLoopChecklist = "agent.loop.checklist";
    public const string AgentLoopEnable = "agent.loop.enable";
    public const string AgentLoopActivate = "agent.loop.activate";
    public const string AgentLoopCancel = "agent.loop.cancel";
    public const string AgentLoopTerminal = "agent.loop.terminal";
    public const string AgentLoopPhase = "agent.loop.phase";
    public const string AgentStop = "agent.stop";
    public const string AgentSessions = "agent.sessions";
    public const string AgentStartBound = "agent.start-bound";
    public const string AgentPicker = "agent.picker";
    public const string AgentLoginUrl = "agent.login-url";
    public const string AgentLoginCode = "agent.login-code";
    public const string AgentSubmitCode = "agent.submit-code";
    public const string AgentTranscript = "agent.transcript";
    public const string AgentMessage = "agent.message";
    public const string AgentSend = "agent.send";
    public const string AgentRun = "agent.run";

    public const string GitPanel = "git.panel";
    public const string GitBranch = "git.branch";
    public const string GitCreateBranchOpen = "git.branch.create-open";
    public const string GitNewBranchName = "git.branch.name";
    public const string GitCreateBranch = "git.branch.create";
    public const string GitCancelCreateBranch = "git.branch.create-cancel";
    public const string GitRefresh = "git.refresh";
    public const string GitFetch = "git.fetch";
    public const string GitPull = "git.pull";
    public const string GitPush = "git.push";
    public const string GitDiscard = "git.discard";
    public const string GitHistory = "git.history";
    public const string GitHistoryBack = "git.history.back";
    public const string GitHosting = "git.hosting";
    public const string GitHostingTitle = "git.hosting.title";
    public const string GitHostingRemedy = "git.hosting.remedy";
    public const string GitHostingSignIn = "git.hosting.sign-in";
    public const string GitHostingOpenSignIn = "git.hosting.open-sign-in";
    public const string GitHostingChecks = "git.hosting.checks";
    public const string GitCommitMessage = "git.commit-message";
    public const string GitCommit = "git.commit";
    public const string GitConfirmCancel = "git.confirm.cancel";
    public const string GitConfirmAccept = "git.confirm.accept";
    public const string GitDiffClose = "git.diff.close";
    public const string GitDiffGoto = "git.diff.goto";

    public const string ValidationPanel = "validation.panel";
    public const string ValidationToggle = "validation.toggle";
    public const string ValidationToggleCollapsed = "validation.toggle.collapsed";
    public const string ValidationFlows = "validation.flows";

    public const string AddWorkspaceRepository = "workspace.add.repository";
    public const string AddWorkspaceBranch = "workspace.add.branch";
    public const string AddWorkspaceDerived = "workspace.add.derived";
    public const string AddWorkspaceCancel = "workspace.add.cancel";
    public const string AddWorkspaceConfirm = "workspace.add.confirm";
    public const string RefreshReviewRevision = "workspace.refresh.revision";
    public const string RefreshReviewCancel = "workspace.refresh.cancel";
    public const string RefreshReviewConfirm = "workspace.refresh.confirm";
    public const string WorkspaceDeleteConfirm = "workspace.delete.confirm-check";
    public const string WorkspaceDeleteCancel = "workspace.delete.cancel";
    public const string WorkspaceDeleteAccept = "workspace.delete.accept";

    public const string AuditSearch = "audit.search";
    public const string AuditRefresh = "audit.refresh";
    public const string AuditStreaming = "audit.streaming";
    public const string AuditFirstPage = "audit.page.first";
    public const string AuditPreviousPage = "audit.page.previous";
    public const string AuditNextPage = "audit.page.next";
    public const string AuditLastPage = "audit.page.last";
    public const string ConsoleShowMore = "console.show-more";
    public const string AppSubtabConsole = "app.subtab.console";
    public const string AppSubtabDiagnostics = "app.subtab.diagnostics";
    public const string AppConsolePane = "app.console.pane";
    public const string WorkspaceListItems = "workspace.list.items";
    public const string WorkspaceListItemsCollapsed = "workspace.list.items.collapsed";
    public const string DatabaseList = "database.list";
    public const string DatabaseTables = "database.tables";
    public const string DatabaseRun = "database.run";
    public const string DatabaseSql = "database.sql";
    public const string DatabaseCell = "database.cell";
    public const string GitLog = "git.log";
    public const string GitDiffGo = "git.diff.go";
    public const string GitDiffLines = "git.diff.lines";
    public const string GitDiffDismiss = "git.diff.dismiss";
    public const string CapabilityClose = "capability.close";

    public static string LoginSaved(string id) => $"login.saved.{id}";
    public static string LoginRemoveSaved(string id) => $"login.remove.{id}";
    public static string WorkspaceStart(string id) => $"workspace.start.{id}";
    public static string WorkspaceStop(string id) => $"workspace.stop.{id}";
    public static string WorkspaceRefresh(string id) => $"workspace.refresh.{id}";
    public static string WorkspaceRemove(string id) => $"workspace.remove.{id}";
    public static string ApplicationTab(string name) => $"app.tab.{name}";
    public static string AgentLoopPrecondition(string id) => $"agent.loop.precondition.{id}";
    public static string AgentPickerRow(string agent) => $"agent.picker.{agent}";
    public static string AgentEnable(string agent) => $"agent.enable.{agent}";
    public static string AgentSession(string sessionId) => $"agent.session.{sessionId}";
    public static string AgentOption(string optionId) => $"agent.option.{optionId}";
    public static string AgentThought(string id) => $"agent.thought.{id}";
    public static string AuditPage(int page) => $"audit.page.{page}";
    public static string AuditFilter(string kind) => $"audit.filter.{kind}";
    public static string GitLogRef(string name) => $"git.log.ref.{name}";
    public static string GitDiffJump(string label) => $"git.diff.jump.{label}";
    public static string GitSelect(string path) => $"git.select.{path}";
    public static string GitToggle(string path) => $"git.toggle.{path}";
    public static string GitOpen(string path) => $"git.open.{path}";
    public static string ValidationPlay(string flowId) => $"validation.play.{flowId}";
    public static string CapabilityEnable(string moduleId) => $"capability.enable.{moduleId}";
    public static string CapabilityDisable(string moduleId) => $"capability.disable.{moduleId}";
}
