using System.Reflection;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

/// <summary>
/// The Desktop route points, held against the window they were read from.
/// </summary>
/// <remarks>
/// Desktop is driven by window-relative coordinates rather than by control name, because
/// xdotool and ImageMagick carry no handle on the Avalonia visual tree. That makes a layout
/// change silent: the point lands somewhere else and the run photographs a plausible wrong
/// screen. These are the two halves of that which can be checked without a window - a point
/// outside the captured area can never be right, and the rows the route reads off have to stay
/// on the rows the constants describe.
/// </remarks>
[TestFixture]
public sealed class DesktopScreenGeometryTests
{
    [Test]
    public void Points_everyXLiesInsideTheCapturedWindow()
    {
        var outside = Constants()
            .Where(constant => constant.Key.EndsWith('X'))
            .Where(constant => constant.Value < 0 || constant.Value >= DebugLayout.DesktopScreenshotWidth)
            .Select(constant => $"{constant.Key}={constant.Value}")
            .ToArray();

        Assert.That(outside, Is.Empty, $"The route captures {DebugLayout.DesktopScreenshotWidth}px wide.");
    }

    [Test]
    public void Points_everyYLiesInsideTheCapturedWindow()
    {
        var outside = Constants()
            .Where(constant => constant.Key.EndsWith('Y'))
            .Where(constant => constant.Value < 0 || constant.Value >= DebugLayout.DesktopScreenshotHeight)
            .Select(constant => $"{constant.Key}={constant.Value}")
            .ToArray();

        Assert.That(outside, Is.Empty, $"The route captures {DebugLayout.DesktopScreenshotHeight}px tall.");
    }

    [Test]
    public void GitTreeRowY_walksDownFromTheFirstRowByOnePitch()
    {
        var first = DesktopScreenGeometry.GitTreeRowY(0);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(DesktopScreenGeometry.GitTreeFirstRowY));
            Assert.That(DesktopScreenGeometry.GitTreeRowY(1) - first, Is.EqualTo(DesktopScreenGeometry.GitTreeRowPitch));
            Assert.That(DesktopScreenGeometry.GitTreeRowY(6), Is.LessThan(DebugLayout.DesktopScreenshotHeight),
                "The deepest row the route ticks has to still be on screen.");
        });
    }

    [Test]
    public void Steps_everyDesktopRouteClicksInsideTheWindow()
    {
        var catalog = new ProductScreenCatalog();

        var outside = catalog.Screens(ProductSurface.Desktop)
            .SelectMany(screen => screen.Steps.Select(step => (screen.Id, step)))
            .Where(entry => entry.step.Kind == ScreenStepKind.Click)
            .Where(entry => entry.step.X < 0
                            || entry.step.X >= DebugLayout.DesktopScreenshotWidth
                            || entry.step.Y < 0
                            || entry.step.Y >= DebugLayout.DesktopScreenshotHeight)
            .Select(entry => $"{entry.Id} clicks ({entry.step.X},{entry.step.Y})")
            .ToArray();

        Assert.That(outside, Is.Empty);
    }

    private static IReadOnlyList<KeyValuePair<string, int>> Constants()
    {
        var constants = typeof(DesktopScreenGeometry)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(int))
            .Select(field => new KeyValuePair<string, int>(field.Name, (int)field.GetRawConstantValue()!))
            .ToArray();

        Assert.That(constants, Is.Not.Empty, "The geometry has no constants to check.");
        return constants;
    }
}
