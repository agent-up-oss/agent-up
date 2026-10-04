using Avalonia.Headless.NUnit;
using Avalonia.Media;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

/// <summary>
/// The icon geometry the design system emits, parsed by the renderer that has to draw it.
/// </summary>
/// <remarks>
/// Desktop draws its icons as Unicode glyphs in TextBlocks, Mobile names Ionicons, and the
/// design system drew its own SVG, so none of the three agreed. The geometry is declared once
/// now and emitted for each surface; this is what stops that emission being a string Avalonia
/// cannot actually take.
/// </remarks>
[TestFixture]
public class AgentUpIconGeometryTests
{
    [AvaloniaTest]
    public void Geometry_everyIconParsesAsAStreamGeometry()
    {
        var failures = AgentUpIcons.Geometry
            .Select(icon => Describe(icon.Key, icon.Value))
            .Where(failure => failure is not null)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(AgentUpIcons.Geometry, Is.Not.Empty, "The design system emitted no icons.");
            Assert.That(failures, Is.Empty, string.Join('\n', failures));
        });
    }

    [AvaloniaTest]
    public void Geometry_coversTheWorkspaceTabsAndTheChromeControls()
    {
        Assert.That(
            AgentUpIcons.Geometry.Keys,
            Is.SupersetOf(new[] { "apps", "git", "agents", "settings", "menu", "reload" }));
    }

    [AvaloniaTest]
    public void ViewBoxSize_isTheGridTheGeometryIsDrawnOn()
    {
        Assert.That(AgentUpIcons.ViewBoxSize, Is.EqualTo(24));
    }

    private static string? Describe(string id, string geometry)
    {
        try
        {
            var parsed = StreamGeometry.Parse(geometry);
            return parsed.Bounds.Width > 0 && parsed.Bounds.Height > 0
                ? null
                : $"{id} parsed to an empty geometry.";
        }
        catch (ArgumentException ex)
        {
            return $"{id}: {ex.Message}";
        }
        catch (FormatException ex)
        {
            return $"{id}: {ex.Message}";
        }
    }
}
