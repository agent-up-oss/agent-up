using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class SyntaxHighlightProviderTests
{
    [Test]
    public void A_fence_tag_names_a_grammar_by_id_alias_or_extension()
    {
        Assert.That(SyntaxHighlightProvider.LanguageForFence("typescript"), Is.EqualTo("typescript"));
        Assert.That(SyntaxHighlightProvider.LanguageForFence("bash"), Is.EqualTo("shell"));
        Assert.That(SyntaxHighlightProvider.LanguageForFence("C#"), Is.EqualTo("csharp"));
        Assert.That(SyntaxHighlightProvider.LanguageForFence("yaml"), Is.EqualTo("yaml"));
        Assert.That(SyntaxHighlightProvider.LanguageForFence("kt"), Is.EqualTo("java"), "An extension resolves through the grammar index.");
        Assert.That(SyntaxHighlightProvider.LanguageForFence("brainfuck"), Is.EqualTo("plaintext"));
        Assert.That(SyntaxHighlightProvider.LanguageForFence(null), Is.EqualTo("plaintext"));
        Assert.That(SyntaxHighlightProvider.DetectLanguage("/repo/server.ts"), Is.EqualTo("typescript"));
        Assert.That(SyntaxHighlightProvider.DetectLanguage("/repo/notes.unknownext"), Is.EqualTo("plaintext"));
    }

    [Test]
    public void A_line_splits_into_keywords_strings_numbers_comments_and_punctuation()
    {
        var tokens = SyntaxHighlightProvider.Tokenize("const port = \"4000\" + 1; // listen", "typescript");
        var block = SyntaxHighlightProvider.Tokenize("/* note */ return x;", "typescript");
        var plain = SyntaxHighlightProvider.Tokenize("just words", "plaintext");

        Assert.That(tokens.Single(token => token.Kind == "keyword").Text, Is.EqualTo("const"));
        Assert.That(tokens.Single(token => token.Kind == "string").Text, Is.EqualTo("\"4000\""));
        Assert.That(tokens.Single(token => token.Kind == "number").Text, Is.EqualTo("1"));
        Assert.That(tokens.Last().Kind, Is.EqualTo("comment"));
        Assert.That(string.Concat(tokens.Select(token => token.Text)), Is.EqualTo("const port = \"4000\" + 1; // listen"));
        Assert.That(block[0], Is.EqualTo(new SyntaxToken("comment", "/* note */")));
        Assert.That(plain.Select(token => token.Kind), Is.All.EqualTo("plain"));
        Assert.That(SyntaxHighlightProvider.Tokenize("", "typescript"), Is.Empty);
    }
}
