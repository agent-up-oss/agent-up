using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming.Tests.Features.Viewport.Unit;

[TestFixture]
public sealed class BrowserViewportPresetTests
{
    [Test]
    public void Default_isTheDesktopPreset()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BrowserViewportPreset.Default.Id, Is.EqualTo("desktop"));
            Assert.That(BrowserViewportPreset.Default.Width, Is.EqualTo(1280));
            Assert.That(BrowserViewportPreset.Default.Height, Is.EqualTo(720));
        });
    }

    [TestCase("mobile", 375, 667)]
    [TestCase("tablet", 768, 1024)]
    [TestCase("desktop", 1280, 720)]
    [TestCase("wide", 1440, 900)]
    [TestCase("full-hd", 1920, 1080)]
    public void FindById_returnsTheMatchingPreset(string id, int width, int height)
    {
        var preset = BrowserViewportPreset.Find(id);

        Assert.Multiple(() =>
        {
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset!.Width, Is.EqualTo(width));
            Assert.That(preset.Height, Is.EqualTo(height));
        });
    }

    [Test]
    public void FindById_isCaseInsensitive()
    {
        Assert.That(BrowserViewportPreset.Find("FULL-HD")!.Id, Is.EqualTo("full-hd"));
    }

    [Test]
    public void FindById_returnsNullForAnUnknownId()
    {
        Assert.That(BrowserViewportPreset.Find("watch"), Is.Null);
    }

    [Test]
    public void FindByDimensions_returnsTheMatchingPreset()
    {
        Assert.That(BrowserViewportPreset.Find(768, 1024)!.Id, Is.EqualTo("tablet"));
    }

    [Test]
    public void FindByDimensions_returnsNullWhenNoPresetMatches()
    {
        Assert.That(BrowserViewportPreset.Find(800, 600), Is.Null);
    }

    [Test]
    public void FindByDimensions_doesNotMatchTransposedDimensions()
    {
        Assert.That(BrowserViewportPreset.Find(1024, 768), Is.Null,
            "A portrait tablet is not the landscape preset.");
    }

    [Test]
    public void Standard_presetsHaveUniqueIds()
    {
        var ids = BrowserViewportPreset.Standard.Select(preset => preset.Id).ToArray();

        Assert.That(ids, Is.Unique);
    }
}
