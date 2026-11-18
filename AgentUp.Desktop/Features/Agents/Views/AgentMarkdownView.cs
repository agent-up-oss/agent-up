using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using AgentUp.Desktop.Features.Agents.Models;
using AgentUp.Desktop.Features.Agents.Providers;

namespace AgentUp.Desktop.Features.Agents.Views;

/// <summary>
/// Draws an agent's markdown with the design system's transcript classes: headings,
/// paragraphs, lists, quotes, rules and code blocks. Inline runs cannot carry style classes in
/// Avalonia, so inline code and links take the same tokens the catalog's inline classes use.
/// The blocks are rebuilt whenever the text changes, which is also how a streaming reply grows.
/// </summary>
public sealed class AgentMarkdownView : StackPanel
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<AgentMarkdownView, string?>(nameof(Markdown));

    private string? _rendered;

    public AgentMarkdownView()
    {
        Spacing = 8;
    }

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
            Render();
    }

    private void Render()
    {
        if (string.Equals(_rendered, Markdown, StringComparison.Ordinal) && Children.Count > 0)
            return;
        _rendered = Markdown;
        Children.Clear();
        foreach (var block in AgentMarkdownProvider.Parse(Markdown))
            Children.Add(Block(block));
    }

    private static Control Block(AgentMarkdownBlock block) => block.Kind switch
    {
        AgentMarkdownBlockKind.Heading => Text(block.Inlines, block.Level <= 2 ? ["au-md-heading"] : ["au-md-heading", "au-md-heading--minor"]),
        AgentMarkdownBlockKind.ListItem => ListItem(block),
        AgentMarkdownBlockKind.Quote => new Border { Classes = { "au-md-quote" }, Child = Text(block.Inlines, ["au-md-paragraph"]) },
        AgentMarkdownBlockKind.Code => CodeBlock(block),
        AgentMarkdownBlockKind.Rule => new Border { Classes = { "au-md-rule" }, Height = 1, Background = Brush("AgentUpColorBorderSubtleBrush"), Margin = new Thickness(0, 4) },
        _ => Text(block.Inlines, ["au-md-paragraph"])
    };

    private static Control ListItem(AgentMarkdownBlock block)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var marker = new TextBlock { Classes = { "au-md-marker" }, Text = block.Marker, Margin = new Thickness(0, 0, 8, 0), MinWidth = 14 };
        var text = Text(block.Inlines, ["au-md-paragraph"]);
        Grid.SetColumn(text, 1);
        row.Children.Add(marker);
        row.Children.Add(text);
        return new Border { Classes = { "au-md-list-item" }, Margin = new Thickness(block.Level * 18, 0, 0, 0), Child = row };
    }

    private static Control CodeBlock(AgentMarkdownBlock block)
    {
        var stack = new StackPanel { Spacing = 4 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock { Classes = { "au-md-code-label" }, Text = block.Language ?? "", VerticalAlignment = VerticalAlignment.Center });
        var copy = new Button
        {
            Classes = { "au-button", "au-button--secondary", "au-button--compact" },
            Content = "Copy",
            [Grid.ColumnProperty] = 1,
        };
        Avalonia.Automation.AutomationProperties.SetAutomationId(copy, AgentUp.Desktop.Shared.Providers.DesktopAutomationIds.AgentCodeCopy);
        copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(copy)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(block.Code);
        };
        header.Children.Add(copy);
        stack.Children.Add(header);
        // Code keeps its lines: long ones scroll sideways instead of wrapping into nonsense.
        stack.Children.Add(new Border
        {
            Classes = { "au-md-code-block" },
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = new TextBlock { Classes = { "au-md-code-text" }, Text = block.Code, TextWrapping = TextWrapping.NoWrap }
            }
        });
        return stack;
    }

    private static TextBlock Text(IReadOnlyList<AgentMarkdownInline> inlines, string[] classes)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Stretch, Inlines = [] };
        foreach (var name in classes)
            text.Classes.Add(name);
        foreach (var inline in inlines)
            text.Inlines!.Add(Inline(inline));
        return text;
    }

    private static Inline Inline(AgentMarkdownInline inline) => inline.Kind switch
    {
        AgentMarkdownInlineKind.Bold => new Run(inline.Text) { FontWeight = FontWeight.SemiBold },
        AgentMarkdownInlineKind.Italic => new Run(inline.Text) { FontStyle = FontStyle.Italic },
        AgentMarkdownInlineKind.Code => new Run($" {inline.Text} ")
        {
            FontFamily = Resource<FontFamily>("AgentUpFontMono") ?? FontFamily.Default,
            Background = Brush("AgentUpColorSurfaceRaisedBrush")
        },
        AgentMarkdownInlineKind.Link => new Run(inline.Text)
        {
            Foreground = Brush("AgentUpColorTextInfoBrush"),
            TextDecorations = TextDecorations.Underline
        },
        _ => new Run(inline.Text)
    };

    private static IBrush? Brush(string key) => Resource<IBrush>(key);

    private static T? Resource<T>(string key) where T : class =>
        Application.Current is { } application && application.TryFindResource(key, application.ActualThemeVariant, out var value)
            ? value as T
            : null;
}
