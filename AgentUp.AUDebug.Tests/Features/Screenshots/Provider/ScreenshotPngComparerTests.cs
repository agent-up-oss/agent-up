using AgentUp.AUDebug.Features.Screenshots.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Providers;
using AgentUp.AUDebug.Shared.Providers;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Provider;

[TestFixture]
public sealed class ScreenshotPngComparerTests
{
    [Test]
    public void Compare_identicalRgb_matches()
    {
        var root = Temp();
        var expected = Path.Join(root, "expected.png");
        var actual = Path.Join(root, "actual.png");
        var png = ScreenshotPngCodec.EncodeRgb(2, 1, [10, 20, 30, 40, 50, 60]);
        File.WriteAllBytes(expected, png);
        File.WriteAllBytes(actual, png);

        var result = new ScreenshotPngComparer().Compare(expected, actual);

        Assert.That(result.Match, Is.True);
        Assert.That(result.DifferingPixels, Is.EqualTo(0));
    }

    [Test]
    public void Compare_changedPixel_fails()
    {
        var root = Temp();
        var expected = Path.Join(root, "expected.png");
        var actual = Path.Join(root, "actual.png");
        File.WriteAllBytes(expected, ScreenshotPngCodec.EncodeRgb(1, 1, [10, 20, 30]));
        File.WriteAllBytes(actual, ScreenshotPngCodec.EncodeRgb(1, 1, [11, 20, 30]));

        var result = new ScreenshotPngComparer().Compare(expected, actual);

        Assert.That(result.Match, Is.False);
        Assert.That(result.DifferingPixels, Is.EqualTo(1));
    }

    [Test]
    public void Compare_missingPersisted_fails()
    {
        var root = Temp();
        var result = new ScreenshotPngComparer().Compare(Path.Join(root, "missing.png"), Path.Join(root, "also-missing.png"));
        Assert.That(result.Match, Is.False);
        Assert.That(result.Detail, Does.Contain("screenshots persist"));
    }

    [Test]
    public void EncodeDecode_roundTripsRgb()
    {
        var root = Temp();
        var path = Path.Join(root, "round.png");
        File.WriteAllBytes(path, ScreenshotPngCodec.EncodeRgb(2, 2, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]));
        var pixels = ScreenshotPngCodec.DecodeRgba(path, out var width, out var height);

        Assert.That(width, Is.EqualTo(2));
        Assert.That(height, Is.EqualTo(2));
        Assert.That(pixels[0], Is.EqualTo(1));
        Assert.That(pixels[3], Is.EqualTo(255));
        Assert.That(pixels[4], Is.EqualTo(4));
    }

    private static string Temp()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-png", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
