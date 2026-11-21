using System.Text.RegularExpressions;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// Reads a reviewer's verdict from what it wrote. A coding agent has no review tool of its own,
/// so its role asks it to end with <c>VERDICT: APPROVE</c> or <c>VERDICT: REQUEST_CHANGES</c>;
/// anything else is a comment, which never approves. "The VERDICT is APPROVE" counts as well: a
/// small model often writes the line as a sentence, and the verdict word must still follow
/// directly, so "the VERDICT is not APPROVE" does not.
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
        return (verdict, WithoutVerdictSentences(text));
    }

    /// <summary>
    /// The review without the sentences that state the verdict, which the host shows as the
    /// review's state instead. A sentence runs back to the previous full stop or line break.
    /// </summary>
    private static string WithoutVerdictSentences(string text)
    {
        var body = text;
        foreach (var match in Verdict().Matches(text).Reverse())
        {
            var start = body.LastIndexOfAny(['.', '!', '?', '\n'], Math.Max(0, match.Index - 1)) + 1;
            var end = match.Index + match.Length;
            if (end < body.Length && body[end] == '.')
                end++;
            body = body[..start] + body[end..];
        }

        return body.Trim();
    }

    /// <summary>The neutral event name the forge modules accept for a verdict.</summary>
    public static string Event(TaskReviewVerdict verdict) => verdict switch
    {
        TaskReviewVerdict.Approve => "APPROVE",
        TaskReviewVerdict.RequestChanges => "REQUEST_CHANGES",
        _ => "COMMENT"
    };

    [GeneratedRegex(@"\**VERDICT\**\s*(?::|\bis\b)\s*\**\s*(?<verdict>APPROVED?|REQUEST[_ -]CHANGES)\b\**", RegexOptions.IgnoreCase)]
    private static partial Regex Verdict();
}
