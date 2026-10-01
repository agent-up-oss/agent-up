using AgentUp.AUDebug.Features.Screenshots.Providers;

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
    public void Compare_missingRegenerated_fails()
    {
        var root = Temp();
        var expected = Path.Join(root, "expected.png");
        File.WriteAllBytes(expected, ScreenshotPngCodec.EncodeRgb(1, 1, [1, 2, 3]));
        var result = new ScreenshotPngComparer().Compare(expected, Path.Join(root, "missing.png"));
        Assert.That(result.Match, Is.False);
        Assert.That(result.Detail, Does.Contain("Regenerated screenshot"));
    }

    [Test]
    public void Compare_sizeMismatch_fails()
    {
        var root = Temp();
        var expected = Path.Join(root, "expected.png");
        var actual = Path.Join(root, "actual.png");
        File.WriteAllBytes(expected, ScreenshotPngCodec.EncodeRgb(1, 1, [1, 2, 3]));
        File.WriteAllBytes(actual, ScreenshotPngCodec.EncodeRgb(2, 1, [1, 2, 3, 4, 5, 6]));
        var result = new ScreenshotPngComparer().Compare(expected, actual);
        Assert.That(result.Match, Is.False);
        Assert.That(result.Detail, Does.Contain("does not match persisted"));
    }

    [Test]
    public void Compare_samePixelsDifferentBytes_matches()
    {
        var root = Temp();
        var expected = Path.Join(root, "expected.png");
        var actual = Path.Join(root, "actual.png");
        var png = ScreenshotPngCodec.EncodeRgb(2, 1, [8, 16, 24, 32, 40, 48]);
        File.WriteAllBytes(expected, png);
        File.WriteAllBytes(actual, WithEmptyIdat(png));
        var result = new ScreenshotPngComparer().Compare(expected, actual);
        Assert.That(result.Match, Is.True);
        Assert.That(result.DifferingPixels, Is.EqualTo(0));
        Assert.That(result.Detail, Is.EqualTo("identical pixels"));
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

    private static byte[] WithEmptyIdat(byte[] png)
    {
        var copy = new byte[png.Length + 12];
        Buffer.BlockCopy(png, 0, copy, 0, png.Length - 12);
        var type = "IDAT"u8.ToArray();
        Buffer.BlockCopy(type, 0, copy, png.Length - 12 + 4, 4);
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        crc ^= 0xFFFFFFFF;
        copy[png.Length - 4] = (byte)(crc >> 24);
        copy[png.Length - 3] = (byte)(crc >> 16);
        copy[png.Length - 2] = (byte)(crc >> 8);
        copy[png.Length - 1] = (byte)crc;
        Buffer.BlockCopy(png, png.Length - 12, copy, png.Length, 12);
        return copy;
    }

    private static readonly uint[] CrcTable = CreateCrc();

    private static uint[] CreateCrc()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) == 1 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }

    private static string Temp()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-png", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
