using System.Text.RegularExpressions;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// Reads a reviewer's verdict from what it wrote. A coding agent has no review tool of its own,
/// so its role asks it to end with <c>VERDICT: APPROVE</c> or <c>VERDICT: REQUEST_CHANGES</c>;
/// anything else is a comment, which never approves.
/// </summary>
public static partial class ReviewVerdictText
{
    public static (TaskReviewVerdict Verdict, string Body) Read(string? reply)
    {
        var text = (reply ?? string.Empty).Trim();
        var matches = Verdict().Matches(text);
        if (matches.Count == 0)
            return (TaskReviewVerdict.Comment, text);
        var last = matches[^1];
        var verdict = last.Groups["verdict"].Value.Replace('-', '_').ToUpperInvariant() switch
        {
            "APPROVE" or "APPROVED" => TaskReviewVerdict.Approve,
            _ => TaskReviewVerdict.RequestChanges
        };
        var body = Verdict().Replace(text, string.Empty).Trim();
        return (verdict, body);
    }

    /// <summary>The neutral event name the forge modules accept for a verdict.</summary>
    public static string Event(TaskReviewVerdict verdict) => verdict switch
    {
        TaskReviewVerdict.Approve => "APPROVE",
        TaskReviewVerdict.RequestChanges => "REQUEST_CHANGES",
        _ => "COMMENT"
    };

    [GeneratedRegex(@"\**VERDICT\**\s*:\s*\**\s*(?<verdict>APPROVED?|REQUEST[_ -]CHANGES)\b\**", RegexOptions.IgnoreCase)]
    private static partial Regex Verdict();
}
