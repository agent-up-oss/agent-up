using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.Providers;

internal sealed record CommitsQueueJson(
    int Version,
    List<CommitEntryJson> Commits,
    CommitEditSessionJson? ActiveSession,
    List<ArchivedCommitEntryJson>? Archive)
{
    public CommitsQueue ToModel() => new(
        Version,
        Commits.Select(e => e.ToModel()).ToList(),
        ActiveSession?.ToModel(),
        Archive?.Select(a => a.ToModel()).ToList());

    public static CommitsQueueJson FromModel(CommitsQueue q) => new(
        Math.Max(2, q.Version),
        q.Commits.Select(CommitEntryJson.FromModel).ToList(),
        q.ActiveSession is null ? null : CommitEditSessionJson.FromModel(q.ActiveSession),
        q.Archived.Select(ArchivedCommitEntryJson.FromModel).ToList());
}

internal sealed record CommitEditSessionJson(string EntryId, string OriginalPatchKey, List<string> Files)
{
    public CommitEditSession ToModel() => new(EntryId, OriginalPatchKey, Files);
    public static CommitEditSessionJson FromModel(CommitEditSession session) => new(session.EntryId, session.OriginalPatchKey, [.. session.Files]);
}

internal sealed record ArchivedCommitEntryJson(CommitEntryJson Entry, string ArchivedAt)
{
    public ArchivedCommitEntry ToModel() => new(Entry.ToModel(), ArchivedAt);
    public static ArchivedCommitEntryJson FromModel(ArchivedCommitEntry archived) => new(CommitEntryJson.FromModel(archived.Entry), archived.ArchivedAt);
}
