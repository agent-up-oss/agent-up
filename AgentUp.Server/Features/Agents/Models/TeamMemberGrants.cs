namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// The fixed capability matrix for a team member kind. This is the single definition of who may
/// read, write, drive the browser, and raise review threads that another member must address.
/// Every enforcement point (ACP permission policy, MCP tool surface, forge review writes, agent
/// binding) reads it from here rather than restating the rule.
/// </summary>
public sealed record TeamMemberGrants(
    TeamMemberKind Kind,
    bool ReadsRepository,
    bool WritesRepository,
    bool ControlsBrowser,
    bool SubmitsReviews,
    AgentFixtureKind Fixture,
    IReadOnlySet<string> OperationPermissions)
{
    // Operation permission keys, spelled as the Authentication slice's OperationPermissions
    // vocabulary so the MCP tool map can filter by them.
    private const string WorkspaceRead = "workspace.read";
    private const string WorkspaceStart = "workspace.start";
    private const string WorkspaceStop = "workspace.stop";
    private const string ApplicationRead = "application.read";
    private const string ApplicationOperate = "application.operate";
    private const string GitRead = "git.read";
    private const string GitWrite = "git.write";
    private const string DiagnosticsRead = "diagnostics.read";
    private const string BrowserControl = "browser.control";

    public static TeamMemberGrants Author { get; } = new(
        TeamMemberKind.Author,
        ReadsRepository: true,
        WritesRepository: true,
        ControlsBrowser: true,
        SubmitsReviews: false,
        AgentFixtureKind.CodingAgent,
        Set(WorkspaceRead, WorkspaceStart, WorkspaceStop, ApplicationRead, ApplicationOperate,
            GitRead, GitWrite, DiagnosticsRead, BrowserControl));

    public static TeamMemberGrants DevReviewer { get; } = new(
        TeamMemberKind.DevReviewer,
        ReadsRepository: true,
        WritesRepository: false,
        ControlsBrowser: false,
        SubmitsReviews: true,
        AgentFixtureKind.CodingAgent,
        Set(WorkspaceRead, ApplicationRead, GitRead, DiagnosticsRead));

    public static TeamMemberGrants PmReviewer { get; } = new(
        TeamMemberKind.PmReviewer,
        ReadsRepository: false,
        WritesRepository: false,
        ControlsBrowser: true,
        SubmitsReviews: true,
        AgentFixtureKind.InProcess,
        Set(WorkspaceRead, ApplicationRead, ApplicationOperate, BrowserControl));

    public static TeamMemberGrants For(TeamMemberKind kind) => kind switch
    {
        TeamMemberKind.DevReviewer => DevReviewer,
        TeamMemberKind.PmReviewer => PmReviewer,
        _ => Author
    };

    /// <summary>
    /// Why an allow rule exceeds this member, or null when the rule is within its grant. Deny
    /// rules never exceed anything. An unscoped execute rule is a write in disguise, so a member
    /// that cannot write may only allow named commands.
    /// </summary>
    public string? Exceeds(PermissionRule rule)
    {
        if (!rule.Allow)
            return null;
        return rule.Kind switch
        {
            PermissionRuleKind.WorktreeRead when !ReadsRepository =>
                $"Rule '{rule.Id}' allows reading the worktree, which a {Kind} may not do.",
            PermissionRuleKind.WorktreeWrite when !WritesRepository =>
                $"Rule '{rule.Id}' allows writing the worktree, which a {Kind} may not do.",
            PermissionRuleKind.Execute when !ReadsRepository =>
                $"Rule '{rule.Id}' allows executing commands, which a {Kind} may not do.",
            PermissionRuleKind.Execute when !WritesRepository && string.IsNullOrWhiteSpace(rule.Scope) =>
                $"Rule '{rule.Id}' allows any command, which can write the worktree. A {Kind} may only allow named commands.",
            _ => null
        };
    }

    public bool AllowsFixture(AgentFixtureKind fixture) => fixture == Fixture;

    private static IReadOnlySet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);
}
