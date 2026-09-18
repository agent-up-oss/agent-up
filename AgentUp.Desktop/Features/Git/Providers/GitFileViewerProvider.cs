using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Features.Git.Providers;

public static class GitFileViewerProvider
{
    public const double LineHeight = 22;
    private const string Operators = "+-*/%=<>!&|^~?:";
    private const string Punctuation = "(){}[];,.";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly Lazy<GitFileViewerGrammarIndex> Grammars = new(LoadGrammars);
    private static readonly Regex HunkHeader = new(@"^@@\s+-(\d+)(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s@@", RegexOptions.CultureInvariant);

    public static IReadOnlyList<GitFileViewerLine> ParseDiff(string? diff)
    {
        if (string.IsNullOrEmpty(diff))
            return [];

        var raw = SplitDiffLines(diff);
        var lines = new List<GitFileViewerLine>(raw.Count);
        var oldNumber = 0;
        var newNumber = 0;
        foreach (var text in raw)
        {
            var index = lines.Count;
            if (text.StartsWith("@@", StringComparison.Ordinal))
            {
                var match = HunkHeader.Match(text);
                if (match.Success)
                {
                    oldNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    newNumber = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                }

                lines.Add(new GitFileViewerLine(index, "hunk", null, null, " ", text));
                continue;
            }

            if (IsMeta(text))
            {
                lines.Add(new GitFileViewerLine(index, "meta", null, null, " ", text));
                continue;
            }

            if (text.StartsWith('+'))
            {
                lines.Add(new GitFileViewerLine(index, "added", null, newNumber, "+", text[1..]));
                newNumber += 1;
                continue;
            }

            if (text.StartsWith('-'))
            {
                lines.Add(new GitFileViewerLine(index, "deleted", oldNumber, null, "−", text[1..]));
                oldNumber += 1;
                continue;
            }

            var body = text.StartsWith(' ') ? text[1..] : text;
            lines.Add(new GitFileViewerLine(index, "context", oldNumber, newNumber, " ", body));
            oldNumber += 1;
            newNumber += 1;
        }

        return lines;
    }

    public static IReadOnlyList<GitFileViewerHunk> Hunks(IReadOnlyList<GitFileViewerLine> lines)
        => lines.Where(line => line.Kind == "hunk")
            .Select(line => new GitFileViewerHunk(line.Text, line.Index))
            .ToList();

    public static IReadOnlyList<string> SplitDiffLines(string diff)
    {
        var lines = new List<string>();
        var start = 0;
        for (var index = 0; index < diff.Length; index++)
        {
            if (diff[index] != '\n')
                continue;
            var end = index > start && diff[index - 1] == '\r' ? index - 1 : index;
            lines.Add(diff[start..end]);
            start = index + 1;
        }

        if (start < diff.Length)
            lines.Add(diff[start..]);
        return lines;
    }

    public static string DetectLanguage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "plaintext";
        var name = System.IO.Path.GetFileName(path);
        if (name is "Dockerfile" or "Makefile")
            return "shell";
        var extension = System.IO.Path.GetExtension(name);
        if (string.IsNullOrEmpty(extension))
            return "plaintext";
        return Grammars.Value.ByExtension.TryGetValue(extension, out var id) ? id : "plaintext";
    }

    public static IReadOnlyList<GitFileViewerToken> Highlight(string path, GitFileViewerLine line)
    {
        if (line.Kind is "hunk" or "meta")
            return [new GitFileViewerToken("plain", line.Text)];
        return Tokenize(line.Text, DetectLanguage(path));
    }

    public static int? JumpIndex(IReadOnlyList<GitFileViewerLine> lines, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
            return null;
        if (int.TryParse(trimmed, CultureInfo.InvariantCulture, out var number) && number.ToString(CultureInfo.InvariantCulture) == trimmed)
        {
            var match = lines.FirstOrDefault(line => line.NewNumber == number || line.OldNumber == number);
            return match?.Index;
        }

        var hunk = lines.FirstOrDefault(line => line.Kind == "hunk" && line.Text.Contains(trimmed, StringComparison.Ordinal));
        return hunk?.Index;
    }

    public static IReadOnlyList<GitFileViewerToken> Tokenize(string text, string language)
    {
        if (string.IsNullOrEmpty(text))
            return [];
        if (!Grammars.Value.ById.TryGetValue(language, out var grammar))
            return [new GitFileViewerToken("plain", text)];

        var keywords = grammar.Keywords.ToHashSet(StringComparer.Ordinal);
        var types = grammar.Types.ToHashSet(StringComparer.Ordinal);
        var tokens = new List<GitFileViewerToken>();
        var index = 0;

        void Push(string kind, string value)
        {
            if (value.Length == 0)
                return;
            if (tokens.Count > 0 && tokens[^1].Kind == kind)
                tokens[^1] = tokens[^1] with { Text = tokens[^1].Text + value };
            else
                tokens.Add(new GitFileViewerToken(kind, value));
        }

        while (index < text.Length)
        {
            var rest = text[index..];
            if (!string.IsNullOrEmpty(grammar.LineComment) && rest.StartsWith(grammar.LineComment, StringComparison.Ordinal))
            {
                Push("comment", text[index..]);
                break;
            }

            if (grammar.BlockComment is { Length: 2 } block && rest.StartsWith(block[0], StringComparison.Ordinal))
            {
                var end = text.IndexOf(block[1], index + block[0].Length, StringComparison.Ordinal);
                var close = end < 0 ? text.Length : end + block[1].Length;
                Push("comment", text[index..close]);
                index = close;
                continue;
            }

            var quote = grammar.Strings.FirstOrDefault(item => rest.StartsWith(item, StringComparison.Ordinal));
            if (quote is not null)
            {
                var cursor = index + quote.Length;
                while (cursor < text.Length)
                {
                    if (text[cursor] == '\\' && cursor + 1 < text.Length)
                    {
                        cursor += 2;
                        continue;
                    }

                    if (text.AsSpan(cursor).StartsWith(quote, StringComparison.Ordinal))
                    {
                        cursor += quote.Length;
                        break;
                    }

                    cursor += 1;
                }

                Push("string", text[index..cursor]);
                index = cursor;
                continue;
            }

            var current = text[index];
            if (char.IsAsciiDigit(current) || (current == '.' && index + 1 < text.Length && char.IsAsciiDigit(text[index + 1])))
            {
                var cursor = index + 1;
                if (current == '0' && cursor < text.Length && (text[cursor] is 'x' or 'X'))
                    cursor += 1;
                while (cursor < text.Length && IsNumberPart(text[cursor]))
                    cursor += 1;
                Push("number", text[index..cursor]);
                index = cursor;
                continue;
            }

            if (IsIdentStart(current))
            {
                var cursor = index + 1;
                while (cursor < text.Length && IsIdentPart(text[cursor]))
                    cursor += 1;
                var word = text[index..cursor];
                var kind = "plain";
                if (keywords.Contains(word))
                    kind = "keyword";
                else if (types.Contains(word))
                    kind = "type";
                else
                {
                    var look = cursor;
                    while (look < text.Length && text[look] == ' ')
                        look += 1;
                    if (look < text.Length && text[look] == '(')
                        kind = "function";
                    else if (index > 0 && text[index - 1] == '.')
                        kind = "property";
                }

                Push(kind, word);
                index = cursor;
                continue;
            }

            if (Operators.Contains(current))
            {
                Push("operator", current.ToString());
                index += 1;
                continue;
            }

            if (Punctuation.Contains(current))
            {
                Push("punctuation", current.ToString());
                index += 1;
                continue;
            }

            Push("plain", current.ToString());
            index += 1;
        }

        return tokens;
    }

    private static GitFileViewerGrammarIndex LoadGrammars()
    {
        var file = JsonSerializer.Deserialize<GitFileViewerGrammarFile>(AgentUpSyntaxGrammars.Json, JsonOptions)
            ?? throw new InvalidOperationException("Design-system syntax grammars are missing.");
        var byId = new Dictionary<string, GitFileViewerLanguageGrammar>(StringComparer.Ordinal);
        var byExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in file.Languages)
        {
            byId[language.Id] = language;
            foreach (var extension in language.Extensions)
                byExtension[extension] = language.Id;
        }

        return new GitFileViewerGrammarIndex(byId, byExtension);
    }

    private static bool IsMeta(string text)
        => text.StartsWith("diff --git", StringComparison.Ordinal)
           || text.StartsWith("index ", StringComparison.Ordinal)
           || text.StartsWith("--- ", StringComparison.Ordinal)
           || text.StartsWith("+++ ", StringComparison.Ordinal)
           || text.StartsWith("new file", StringComparison.Ordinal)
           || text.StartsWith("deleted file", StringComparison.Ordinal)
           || text.StartsWith("similarity index", StringComparison.Ordinal)
           || text.StartsWith("rename ", StringComparison.Ordinal)
           || text.StartsWith("\\ ", StringComparison.Ordinal);

    private static bool IsIdentStart(char character)
        => character is '_' or '$' || char.IsAsciiLetter(character);

    private static bool IsIdentPart(char character) => IsIdentStart(character) || char.IsAsciiDigit(character);

    private static bool IsNumberPart(char character)
        => char.IsAsciiHexDigit(character) || character is '_' or '.' or 'n';
}

internal sealed record GitFileViewerGrammarFile(GitFileViewerLanguageGrammar[] Languages);

internal sealed record GitFileViewerLanguageGrammar(
    string Id,
    string[] Extensions,
    string[] Keywords,
    string[] Types,
    string LineComment,
    string[]? BlockComment,
    string[] Strings);

internal sealed record GitFileViewerGrammarIndex(
    Dictionary<string, GitFileViewerLanguageGrammar> ById,
    Dictionary<string, string> ByExtension);

public sealed record GitFileViewerLine(
    int Index,
    string Kind,
    int? OldNumber,
    int? NewNumber,
    string Prefix,
    string Text);

public sealed record GitFileViewerToken(string Kind, string Text);

public sealed record GitFileViewerHunk(string Label, int LineIndex);
