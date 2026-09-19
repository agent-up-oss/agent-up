using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Providers;
using AgentUp.Desktop.Shared.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AgentUp.Desktop.Features.Git.Views;

public sealed class GitLogGraphColumn : Control
{
    private static readonly string[] LaneColors =
    [
        AgentUpThemeColors.GitLane0,
        AgentUpThemeColors.GitLane1,
        AgentUpThemeColors.GitLane2,
        AgentUpThemeColors.GitLane3,
        AgentUpThemeColors.GitLane4,
        AgentUpThemeColors.GitLane5,
        AgentUpThemeColors.GitLane6,
        AgentUpThemeColors.GitLane7,
    ];

    public static readonly StyledProperty<GitLogRowDto?> RowProperty =
        AvaloniaProperty.Register<GitLogGraphColumn, GitLogRowDto?>(nameof(Row));

    public GitLogRowDto? Row
    {
        get => GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    static GitLogGraphColumn()
    {
        AffectsRender<GitLogGraphColumn>(RowProperty);
        AffectsMeasure<GitLogGraphColumn>(RowProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var row = Row;
        if (row is null)
            return new Size(GitLogLayoutProvider.LaneWidth, GitLogLayoutProvider.RowHeight);
        return new Size(GitLogLayoutProvider.GraphWidth(row.LaneCount), GitLogLayoutProvider.RowHeight);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var row = Row;
        if (row is null)
            return;

        var mid = GitLogLayoutProvider.RowHeight / 2;
        foreach (var lane in row.IncomingLanes)
        {
            var x = GitLogLayoutProvider.LaneX(lane);
            DrawLine(context, new Point(x, 0), new Point(x, mid), LaneBrush(lane));
        }

        foreach (var link in row.Outgoing)
        {
            var x1 = GitLogLayoutProvider.LaneX(link.FromLane);
            var x2 = GitLogLayoutProvider.LaneX(link.ToLane);
            var brush = LaneBrush(link.ColorLane);
            if (link.FromLane == link.ToLane)
            {
                DrawLine(context, new Point(x1, mid), new Point(x2, GitLogLayoutProvider.RowHeight), brush);
                continue;
            }

            var cy = (mid + GitLogLayoutProvider.RowHeight) / 2;
            var geometry = new StreamGeometry();
            using (var geo = geometry.Open())
            {
                geo.BeginFigure(new Point(x1, mid), false);
                geo.CubicBezierTo(new Point(x1, cy), new Point(x2, cy), new Point(x2, GitLogLayoutProvider.RowHeight));
                geo.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(brush, 2) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, geometry);
        }

        var nodeX = GitLogLayoutProvider.LaneX(row.Lane);
        var laneBrush = LaneBrush(row.Lane);
        var isHead = row.Refs.Any(item => item.Kind == "head");
        context.DrawEllipse(
            isHead ? Brush(AgentUpThemeColors.Canvas) : laneBrush,
            new Pen(laneBrush, 2),
            new Point(nodeX, mid),
            GitLogLayoutProvider.NodeRadius,
            GitLogLayoutProvider.NodeRadius);
    }

    private static void DrawLine(DrawingContext context, Point start, Point end, IBrush brush)
        => context.DrawLine(new Pen(brush, 2) { LineCap = PenLineCap.Round }, start, end);

    private static IBrush LaneBrush(int lane)
        => Brush(LaneColors[((lane % LaneColors.Length) + LaneColors.Length) % LaneColors.Length]);

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
