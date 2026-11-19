using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using AgentUp.Desktop.Features.Agents.Models;
using AgentUp.Desktop.Features.Agents.Providers;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Agents.Views;

/// <summary>
/// Draws an agent's markdown with the design system's transcript classes: headings,
/// paragraphs, lists, quotes, rules, tables and highlighted code. Inline runs cannot carry
/// style classes in Avalonia, so inline code and links take the same tokens the catalog's
/// inline classes use. A streaming reply only redraws from the first block that changed, so
/// the blocks a new chunk did not touch keep their controls and do not flicker.
/// </summary>
public sealed class AgentMarkdownView : StackPanel
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<AgentMarkdownView, string?>(nameof(Markdown));

    /// <summary>Longer code is drawn as plain text: highlighting it costs more than it shows.</summary>
    public const int HighlightLineLimit = 400;

    private readonly List<string> _keys = [];

    public AgentMarkdownView()
    {
        Spacing = 8;
    }

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>How many block controls the last render rebuilt; the rest were kept.</summary>
    public int LastRebuilt { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
            Render();
    }

    private void Render()
    {
        var blocks = AgentMarkdownProvider.Parse(Markdown);
        var keys = blocks.Select(block => block.Key).ToArray();
        var kept = 0;
        while (kept < keys.Length && kept < _keys.Count && string.Equals(keys[kept], _keys[kept], StringComparison.Ordinal))
            kept++;

        while (Children.Count > kept)
            Children.RemoveAt(Children.Count - 1);
        _keys.RemoveRange(kept, _keys.Count - kept);
        for (var index = kept; index < blocks.Count; index++)
        {
            Children.Add(Block(blocks[index]));
            _keys.Add(keys[index]);
        }

        LastRebuilt = blocks.Count - kept;
    }

    private static Control Block(AgentMarkdownBlock block) => block.Kind switch
    {
        AgentMarkdownBlockKind.Heading => Text(block.Inlines, block.Level <= 2 ? ["au-md-heading"] : ["au-md-heading", "au-md-heading--minor"]),
        AgentMarkdownBlockKind.ListItem => ListItem(block),
        AgentMarkdownBlockKind.Quote => new Border { Classes = { "au-md-quote" }, Child = Text(block.Inlines, ["au-md-paragraph"]) },
        AgentMarkdownBlockKind.Code => CodeBlock(block),
        AgentMarkdownBlockKind.Table => Table(block),
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
        return new Border { Classes = { "au-md-list-item" }, Margin = new Thickness(block.Level * 18.0, 0, 0, 0), Child = row };
    }

    /// <summary>
    /// A code block is one container: a slim header with the language and Copy, then the code.
    /// Keeping Copy inside the block ties it to the code it copies, even with no language label.
    /// </summary>
    private static Control CodeBlock(AgentMarkdownBlock block)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 4) };
        header.Children.Add(new TextBlock { Classes = { "au-md-code-label" }, Text = block.Language ?? "", VerticalAlignment = VerticalAlignment.Center });
        var copy = new Button
        {
            Classes = { "au-button", "au-button--secondary", "au-button--compact" },
            Content = "Copy",
            [Grid.ColumnProperty] = 1,
        };
        Avalonia.Automation.AutomationProperties.SetAutomationId(copy, DesktopAutomationIds.AgentCodeCopy);
        copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(copy)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(block.Code);
        };
        header.Children.Add(copy);
        // Code keeps its lines: long ones scroll sideways instead of wrapping into nonsense.
        var code = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 0, 6),
            Content = CodeLines(block.Code ?? "", SyntaxHighlightProvider.LanguageForFence(block.Language))
        };
        if (!string.IsNullOrEmpty(block.Language))
            return new Border { Classes = { "au-md-code-block" }, Child = new StackPanel { Children = { header, code } } };
        // With no language to label, a header row would be empty: Copy sits in the corner instead.
        header.Children.Remove(copy);
        copy.HorizontalAlignment = HorizontalAlignment.Right;
        copy.VerticalAlignment = VerticalAlignment.Top;
        copy.Margin = new Thickness(0, -4, -4, 0);
        return new Border { Classes = { "au-md-code-block" }, Child = new Panel { Children = { code, copy } } };
    }

    /// <summary>One row of highlighted tokens per line, using the same syntax classes as the Git viewer.</summary>
    private static Control CodeLines(string code, string language)
    {
        var lines = code.Split('\n');
        if (language == "plaintext" || lines.Length > HighlightLineLimit)
            return new TextBlock { Classes = { "au-md-code-text" }, Text = code, TextWrapping = TextWrapping.NoWrap };

        var stack = new StackPanel { Classes = { "au-md-code-lines" } };
        foreach (var line in lines)
        {
            stack.Children.Add(new AgentSyntaxLineView { MinHeight = 18, Tokens = SyntaxHighlightProvider.Tokenize(line, language) });
        }

        return stack;
    }

    private static Control Table(AgentMarkdownBlock block)
    {
        var rows = block.Rows ?? [];
        var columns = rows.Count == 0 ? 0 : rows[0].Count;
        var grid = new Grid();
        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var row = 0; row < rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            // Each row is one band across every column, so the header shading and row rules read whole.
            var band = new Border { Classes = { row == 0 ? "au-md-table-head" : "au-md-table-row" } };
            Grid.SetRow(band, row);
            Grid.SetColumnSpan(band, Math.Max(1, columns));
            grid.Children.Add(band);
            for (var column = 0; column < columns; column++)
            {
                var cell = Text(rows[row][column], [row == 0 ? "au-md-table-header-cell" : "au-md-table-cell"]);
                cell.TextAlignment = block.Alignments?.ElementAtOrDefault(column) switch
                {
                    "center" => TextAlignment.Center,
                    "right" => TextAlignment.Right,
                    _ => TextAlignment.Left
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }

        return new Border
        {
            Classes = { "au-md-table" },
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                // Room under the last row, so a wide table's scrollbar does not sit on top of it.
                Padding = grid.ColumnDefinitions.Count > 0 ? new Thickness(0, 0, 0, 8) : default,
                Content = grid
            }
        };
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
        // No-break padding keeps the code's shading attached to it where a line wraps.
        AgentMarkdownInlineKind.Code => new Run($"\u202f{inline.Text}\u202f")
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
