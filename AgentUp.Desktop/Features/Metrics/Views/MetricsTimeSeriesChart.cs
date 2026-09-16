using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AgentUp.Desktop.Shared.Models;
using AgentUp.Desktop.Features.Metrics.ViewModels;

namespace AgentUp.Desktop.Features.Metrics.Views;

public sealed class MetricsTimeSeriesChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<MetricsPointViewModel>?> PointsProperty =
        AvaloniaProperty.Register<MetricsTimeSeriesChart, IReadOnlyList<MetricsPointViewModel>?>(nameof(Points));

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<MetricsTimeSeriesChart, string>(nameof(Unit), string.Empty);

    public IReadOnlyList<MetricsPointViewModel>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    static MetricsTimeSeriesChart()
    {
        AffectsRender<MetricsTimeSeriesChart>(PointsProperty, UnitProperty);
        HeightProperty.OverrideDefaultValue<MetricsTimeSeriesChart>(190);
        MinHeightProperty.OverrideDefaultValue<MetricsTimeSeriesChart>(150);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        context.DrawRectangle(Brush(AgentUpThemeColors.Surface), null, bounds);

        var points = Points;
        if (points is null || points.Count == 0)
            return;

        const double labelWidth = 44;
        const double padRight = 12;
        const double padTop = 8;
        const double padBottom = 10;
        var plot = new Rect(
            bounds.Left + labelWidth,
            bounds.Top + padTop,
            Math.Max(1, bounds.Width - labelWidth - padRight),
            Math.Max(1, bounds.Height - padTop - padBottom));

        var dataMax = points.Max(point => point.Value);
        var (axisMax, step) = MetricsAxisScaler.Compute(dataMax);
        var gridPen = new Pen(Brush(AgentUpThemeColors.BorderSubtle), 1);
        var typeface = new Typeface(FontFamily.Default);
        var labelBrush = Brush(AgentUpThemeColors.TextMuted);
        var unit = Unit;

        var tickCount = (int)Math.Round(axisMax / step);
        for (var tick = 0; tick <= tickCount; tick++)
        {
            var value = tick * step;
            var normalized = axisMax <= 0 ? 0 : value / axisMax;
            var y = plot.Bottom - normalized * plot.Height;
            context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));

            var label = MetricsAxisScaler.FormatTick(value, unit);
            var formatted = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                labelBrush);
            context.DrawText(
                formatted,
                new Point(plot.Left - formatted.Width - 6, y - formatted.Height / 2));
        }

        var slot = plot.Width / points.Count;
        var barWidth = Math.Max(4, slot * 0.62);
        var accent = Color.Parse(AgentUpThemeColors.StatusHealthy);
        var accentDim = Color.Parse(AgentUpThemeColors.SurfaceSelectedStrong);

        for (var i = 0; i < points.Count; i++)
        {
            var normalized = axisMax <= 0 ? 0 : points[i].Value / axisMax;
            var barHeight = Math.Max(points[i].Value > 0 ? 2 : 0, normalized * plot.Height);
            var x = plot.Left + i * slot + (slot - barWidth) / 2;
            var y = plot.Bottom - barHeight;
            var isLast = i == points.Count - 1;
            context.DrawRectangle(new SolidColorBrush(isLast ? accent : accentDim), null, new Rect(x, y, barWidth, barHeight));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var started = false;
            for (var i = 0; i < points.Count; i++)
            {
                var normalized = axisMax <= 0 ? 0 : points[i].Value / axisMax;
                var x = plot.Left + i * slot + slot / 2;
                var y = plot.Bottom - normalized * plot.Height;
                if (!started)
                {
                    ctx.BeginFigure(new Point(x, y), false);
                    started = true;
                }
                else
                    ctx.LineTo(new Point(x, y));
            }
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(accent), 2), geometry);
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
