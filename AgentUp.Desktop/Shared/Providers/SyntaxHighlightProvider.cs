using System.Text.Json;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Shared.Providers;

/// <summary>One highlighted run of a line: <c>keyword</c>, <c>string</c>, <c>comment</c> and so on.</summary>
public sealed record SyntaxToken(string Kind, string Text);

/// <summary>
/// Line tokenizer over the design system's syntax grammars. The Git file viewer and agent code
/// blocks both highlight with it, so a language reads the same wherever code appears.
/// </summary>
public static class SyntaxHighlightProvider
{
    private const string Operators = "+-*/%=<>!&|^~?:";
    private const string Punctuation = "(){}[];,.";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly Lazy<SyntaxGrammarIndex> Grammars = new(LoadGrammars);

    // What agents write after a fence when it is neither a grammar id nor a file extension.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["javascript"] = "typescript", ["js"] = "typescript", ["ts"] = "typescript", ["tsx"] = "typescript", ["jsx"] = "typescript",
        ["sh"] = "shell", ["bash"] = "shell", ["zsh"] = "shell", ["console"] = "shell", ["shell-session"] = "shell",
        ["py"] = "python", ["cs"] = "csharp", ["c#"] = "csharp", ["yml"] = "yaml", ["kotlin"] = "java",
        ["rb"] = "ruby", ["rs"] = "rust", ["golang"] = "go", ["xml"] = "html", ["md"] = "markdown"
    };

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

    /// <summary>The grammar for a code fence's language tag, or <c>plaintext</c> when none fits.</summary>
    public static string LanguageForFence(string? tag)
    {
        var name = tag?.Trim() ?? "";
        if (name.Length == 0)
            return "plaintext";
        if (Grammars.Value.ById.ContainsKey(name.ToLowerInvariant()))
            return name.ToLowerInvariant();
        if (Aliases.TryGetValue(name, out var alias))
            return alias;
        return Grammars.Value.ByExtension.TryGetValue("." + name, out var id) ? id : "plaintext";
    }

    public static IReadOnlyList<SyntaxToken> Tokenize(string text, string language)
    {
        if (string.IsNullOrEmpty(text))
            return [];
        if (!Grammars.Value.ById.TryGetValue(language, out var grammar))
            return [new SyntaxToken("plain", text)];

        var keywords = grammar.Keywords.ToHashSet(StringComparer.Ordinal);
        var types = grammar.Types.ToHashSet(StringComparer.Ordinal);
        var tokens = new List<SyntaxToken>();
        var index = 0;

        void Push(string kind, string value)
        {
            if (value.Length == 0)
                return;
            if (tokens.Count > 0 && tokens[^1].Kind == kind)
                tokens[^1] = tokens[^1] with { Text = tokens[^1].Text + value };
            else
                tokens.Add(new SyntaxToken(kind, value));
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

    private static SyntaxGrammarIndex LoadGrammars()
    {
        var file = JsonSerializer.Deserialize<SyntaxGrammarFile>(AgentUpSyntaxGrammars.Json, JsonOptions)
            ?? throw new InvalidOperationException("Design-system syntax grammars are missing.");
        var byId = new Dictionary<string, SyntaxLanguageGrammar>(StringComparer.Ordinal);
        var byExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in file.Languages)
        {
            byId[language.Id] = language;
            foreach (var extension in language.Extensions)
                byExtension[extension] = language.Id;
        }

        return new SyntaxGrammarIndex(byId, byExtension);
    }

    private static bool IsIdentStart(char character)
        => character is '_' or '$' || char.IsAsciiLetter(character);

    private static bool IsIdentPart(char character) => IsIdentStart(character) || char.IsAsciiDigit(character);

    private static bool IsNumberPart(char character)
        => char.IsAsciiHexDigit(character) || character is '_' or '.' or 'n';
}

internal sealed record SyntaxGrammarFile(SyntaxLanguageGrammar[] Languages);

internal sealed record SyntaxLanguageGrammar(
    string Id,
    string[] Extensions,
    string[] Keywords,
    string[] Types,
    string LineComment,
    string[]? BlockComment,
    string[] Strings);

internal sealed record SyntaxGrammarIndex(
    Dictionary<string, SyntaxLanguageGrammar> ById,
    Dictionary<string, string> ByExtension);

