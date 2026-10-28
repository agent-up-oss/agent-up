using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>One fact about the work in hand: what is asked, and what answers it.</summary>
public sealed record AgentFact(string Label, string Value);

/// <summary>One file a proposal changes.</summary>
public sealed record AgentChangedFile(string Path, string Status);

/// <summary>
/// What an agent's own page says, apart from how it is drawn.
///
/// The page is composed rather than read: every line here comes from the standing the team already
/// holds and the workspace behind it, so the same words answer against the Demo and against a real
/// Server.
/// </summary>
public static class AgentPageText
{
    /// <summary>
    /// What the agent is working on, in the order the questions come: the task, what it has
    /// proposed for it, and the branch and commit that proposal was built from.
    ///
    /// A fact the Server did not answer is left out rather than carried empty, because a blank row
    /// reads as an answer of nothing rather than as a question nobody asked.
    /// </summary>
    public static IReadOnlyList<AgentFact> Facts(TeamStanding standing, string? branch, string? head)
    {
        var facts = new List<AgentFact>();
        if (standing.TaskNumber is { } number)
            facts.Add(new AgentFact("Task", $"#{number} · {standing.TaskTitle}".TrimEnd(' ', '·')));
        if (standing.Forge.HasProposal)
            facts.Add(new AgentFact("Proposal", Proposal(standing)));

        var onBranch = branch is { Length: > 0 } ? branch : standing.Branch;
        if (onBranch is { Length: > 0 })
            facts.Add(new AgentFact("Branch", onBranch));
        if (head is { Length: > 0 })
            facts.Add(new AgentFact("Head", head));
        return facts;
    }

    private static string Proposal(TeamStanding standing)
    {
        var number = $"#{standing.Forge.ProposalNumber}";
        var account = standing.Identity?.Login;
        return account is { Length: > 0 } ? $"{number} · {account}" : number;
    }

    /// <summary>
    /// Where an application can be reached. A process that was given a port says which one and what
    /// speaks there; one that was not says so, because a blank reads as a port nobody noticed.
    /// </summary>
    public static string Address(ApplicationDto application)
    {
        var port = application.AllocatedPorts.FirstOrDefault();
        if (port is null)
            return string.Equals(application.Kind, "Desktop", StringComparison.OrdinalIgnoreCase)
                ? "desktop · no port"
                : "no port";
        return $":{port.AllocatedPort} · {port.Protocol}";
    }

    /// <summary>
    /// How the review on a proposal stands, counted rather than listed: a reader wants to know
    /// whether anything is waiting on them before they want to know what each thread says.
    /// </summary>
    public static string FindingSummary(TeamStanding standing)
    {
        if (!standing.Forge.HasProposal) return "No proposal open.";
        var threads = standing.Forge.UnresolvedThreads;
        if (threads == 0) return "No unresolved threads.";
        return threads == 1 ? "1 unresolved thread" : $"{threads} unresolved threads";
    }

    /// <summary>
    /// How the checks on a proposal stand, in one line, naming only the states that happened.
    /// </summary>
    public static string CheckSummary(TeamStanding standing)
    {
        var forge = standing.Forge;
        if (!forge.HasProposal || forge.ChecksTotal == 0) return "No checks reported.";

        var parts = new List<string> { $"{forge.ChecksPassed} passed" };
        if (forge.ChecksFailed > 0) parts.Add($"{forge.ChecksFailed} failed");
        if (forge.ChecksRunning > 0) parts.Add($"{forge.ChecksRunning} running");
        if (forge.ChecksSkipped > 0) parts.Add($"{forge.ChecksSkipped} skipped");
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// What the agent is, under its name: the role it was given and the account it acts as. A
    /// workspace with neither is still an agent, so the line falls back to what its loop is doing.
    /// </summary>
    public static string Subtitle(TeamStanding standing)
    {
        var parts = new List<string>();
        if (standing.RoleLabel is { Length: > 0 } role) parts.Add(role);
        if (standing.Identity?.Login is { Length: > 0 } login) parts.Add($"@{login}");
        if (standing.Agent is { Length: > 0 } agent) parts.Add(agent);
        return parts.Count > 0 ? string.Join(" · ", parts) : standing.WorkspaceState;
    }

    /// <summary>
    /// The changed files out of the directory tree the Server owns. The page lists files rather
    /// than folders, because what changed is the thing being asked about.
    /// </summary>
    public static IReadOnlyList<AgentChangedFile> Files(GitChangeTreeDto? tree)
    {
        if (tree is null) return [];
        var files = new List<AgentChangedFile>();
        Collect(tree.Root, files);
        return files;
    }

    private static void Collect(GitChangeDirectoryDto directory, List<AgentChangedFile> files)
    {
        files.AddRange(directory.Files.Select(file => new AgentChangedFile(file.Path, file.Status)));
        foreach (var child in directory.Directories)
            Collect(child, files);
    }
}
