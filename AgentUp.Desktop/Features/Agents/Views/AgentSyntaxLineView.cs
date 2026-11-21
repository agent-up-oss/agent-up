using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Agents.Views;

/// <summary>
/// One line of highlighted code: a text block per token, carrying the design system's
/// <c>au-syntax-*</c> class for its kind plus <see cref="TextClass"/> for the surface it sits on.
/// </summary>
public sealed class AgentSyntaxLineView : StackPanel
{
    public static readonly StyledProperty<IReadOnlyList<SyntaxToken>?> TokensProperty =
        AvaloniaProperty.Register<AgentSyntaxLineView, IReadOnlyList<SyntaxToken>?>(nameof(Tokens));

    public static readonly StyledProperty<string> TextClassProperty =
        AvaloniaProperty.Register<AgentSyntaxLineView, string>(nameof(TextClass), "au-md-code-text");

    public AgentSyntaxLineView()
    {
        Orientation = Orientation.Horizontal;
    }

    public IReadOnlyList<SyntaxToken>? Tokens
    {
        get => GetValue(TokensProperty);
        set => SetValue(TokensProperty, value);
    }

    public string TextClass
    {
        get => GetValue(TextClassProperty);
        set => SetValue(TextClassProperty, value);
    }

    public static IEnumerable<TextBlock> Blocks(IReadOnlyList<SyntaxToken> tokens, string textClass) =>
        tokens.Select(token => new TextBlock { Classes = { textClass, $"au-syntax-{token.Kind}" }, Text = token.Text, TextWrapping = TextWrapping.NoWrap });

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TokensProperty && change.Property != TextClassProperty)
            return;
        Children.Clear();
        foreach (var block in Blocks(Tokens ?? [], TextClass))
            Children.Add(block);
    }
}
