using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming.Tests.Features.Viewport.Unit;

[TestFixture]
public sealed class BrowserControlModeTests
{
    [Test]
    public void DefaultAi_takesTheDefaultPresetDimensions()
    {
        var mode = BrowserControlMode.DefaultAi;

        Assert.Multiple(() =>
        {
            Assert.That(mode.Authority, Is.EqualTo(ControlAuthority.Ai));
            Assert.That(mode.Width, Is.EqualTo(BrowserViewportPreset.Default.Width));
            Assert.That(mode.Height, Is.EqualTo(BrowserViewportPreset.Default.Height));
        });
    }

    [Test]
    public void DefaultHuman_carriesNoFixedViewportSoTheClientDecides()
    {
        var mode = BrowserControlMode.DefaultHuman;

        Assert.Multiple(() =>
        {
            Assert.That(mode.Authority, Is.EqualTo(ControlAuthority.Human));
            Assert.That(mode.Width, Is.Zero);
            Assert.That(mode.Height, Is.Zero);
        });
    }
}
