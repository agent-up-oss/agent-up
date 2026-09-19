using AgentUp.Desktop.Features.Git.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds a <see cref="GitLogCommitDto"/> so Git history tests name the id, parents, and
/// refs they care about instead of restating the seven-field production constructor.
/// </summary>
internal sealed class GitLogCommitDtoBuilder
{
    private string _id = "aaa";
    private string _shortId = "aaa";
    private IReadOnlyList<string> _parents = [];
    private string _subject = "root";
    private string _author = "A";
    private string _timestamp = "2026-01-01T00:00:00Z";
    private IReadOnlyList<string> _refs = [];

    public GitLogCommitDtoBuilder WithId(string id)
    {
        _id = id;
        _shortId = id;
        return this;
    }

    public GitLogCommitDtoBuilder WithParents(params string[] parents)
    {
        _parents = parents;
        return this;
    }

    public GitLogCommitDtoBuilder Saying(string subject)
    {
        _subject = subject;
        return this;
    }

    public GitLogCommitDtoBuilder By(string author)
    {
        _author = author;
        return this;
    }

    public GitLogCommitDtoBuilder At(string timestamp)
    {
        _timestamp = timestamp;
        return this;
    }

    public GitLogCommitDtoBuilder WithRefs(params string[] refs)
    {
        _refs = refs;
        return this;
    }

    public GitLogCommitDto Build()
        => new(_id, _shortId, _parents, _subject, _author, _timestamp, _refs);
}
