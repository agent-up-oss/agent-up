using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Tests.Support;

/// <summary>
/// Builds a <see cref="CommitEntry"/> from the canonical queued commit in
/// <see cref="CliDomain"/>, so a test states only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="CommitEntry"/> positionally. Three of its
/// six fields are optional strings, and reading them positionally tells nobody which commit
/// a test means.
/// </remarks>
internal sealed class CommitEntryBuilder
{
    private string _slice = CliDomain.Slice;
    private string _message = CliDomain.CommitMessage;
    private IReadOnlyList<string> _files = [CliDomain.CommitFile];
    private string _id = "";
    private string _patchId = "";
    private string? _reviewIssueId;

    public CommitEntryBuilder For(string slice)
    {
        _slice = slice;
        return this;
    }

    public CommitEntryBuilder Saying(string message)
    {
        _message = message;
        return this;
    }

    public CommitEntryBuilder Touching(params string[] files)
    {
        _files = files;
        return this;
    }

    public CommitEntryBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public CommitEntryBuilder WithPatchId(string patchId)
    {
        _patchId = patchId;
        return this;
    }

    public CommitEntryBuilder WithReviewIssue(string? reviewIssueId)
    {
        _reviewIssueId = reviewIssueId;
        return this;
    }

    public CommitEntry Build() => new(_slice, _message, _files, _id, _patchId, _reviewIssueId);
}
