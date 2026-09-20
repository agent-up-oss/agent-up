using AgentUp.Server.Features.Commits.DTOs;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Builds an <see cref="EnqueueRequest"/> from the canonical queued commit in
/// <see cref="ServerDomain"/>, so a test states only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="EnqueueRequest"/> positionally.
/// </remarks>
internal sealed class EnqueueRequestBuilder
{
    private string _slice = ServerDomain.Slice;
    private string _message = ServerDomain.CommitMessage;
    private IReadOnlyList<string> _files = [ServerDomain.CommitFile];
    private string? _reviewIssueId;

    public EnqueueRequestBuilder For(string slice)
    {
        _slice = slice;
        return this;
    }

    public EnqueueRequestBuilder Saying(string message)
    {
        _message = message;
        return this;
    }

    public EnqueueRequestBuilder Touching(params string[] files)
    {
        _files = files;
        return this;
    }

    public EnqueueRequestBuilder WithReviewIssue(string? reviewIssueId)
    {
        _reviewIssueId = reviewIssueId;
        return this;
    }

    public EnqueueRequest Build() => new(_slice, _message, _files, _reviewIssueId);
}
