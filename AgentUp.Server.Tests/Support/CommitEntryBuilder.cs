using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Builds a <see cref="CommitEntry"/> from the canonical queued commit in
/// <see cref="ServerDomain"/>, so a test states only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="CommitEntry"/> positionally. The record has
/// nine fields, six of them optional, and reading them positionally tells nobody which
/// commit a test means.
/// </remarks>
internal sealed class CommitEntryBuilder
{
    private string _slice = ServerDomain.Slice;
    private string _message = ServerDomain.CommitMessage;
    private IReadOnlyList<string> _files = [ServerDomain.CommitFile];
    private string _id = "";
    private string _patchId = "";
    private string? _reviewIssueId;
    private string? _parentCommit;
    private string? _proposalCommit;
    private string _state = "draft";

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

    public CommitEntryBuilder WithParentCommit(string? parentCommit)
    {
        _parentCommit = parentCommit;
        return this;
    }

    public CommitEntryBuilder WithProposalCommit(string? proposalCommit)
    {
        _proposalCommit = proposalCommit;
        return this;
    }

    public CommitEntryBuilder InState(string state)
    {
        _state = state;
        return this;
    }

    public CommitEntry Build()
        => new(
            _slice,
            _message,
            _files,
            _id,
            _patchId,
            _reviewIssueId,
            _parentCommit,
            _proposalCommit,
            _state);
}
