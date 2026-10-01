using System.Buffers.Binary;
using System.IO.Compression;
using AgentUp.AUDebug.Features.Screenshots.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Provider;

[TestFixture]
public sealed class ScreenshotPngCodecTests
{
    [Test]
    public void Decode_rejectsNonPngSignature()
    {
        var path = Write("bad.png", [1, 2, 3, 4, 5, 6, 7, 8]);
        Assert.That(
            () => ScreenshotPngCodec.DecodeRgba(path, out _, out _),
            Throws.InvalidOperationException.With.Message.Contains("is not a PNG"));
    }

    [Test]
    public void Decode_rejectsTruncatedFile()
    {
        var path = Write("short.png", [137, 80, 78, 71, 13, 10, 26]);
        Assert.That(
            () => ScreenshotPngCodec.DecodeRgba(path, out _, out _),
            Throws.InvalidOperationException.With.Message.Contains("truncated"));
    }

    [Test]
    public void Decode_rejectsMissingHeader()
    {
        var path = Write("no-header.png", PngWithChunks(Iend()));
        Assert.That(
            () => ScreenshotPngCodec.DecodeRgba(path, out _, out _),
            Throws.InvalidOperationException.With.Message.Contains("missing a PNG header"));
    }

    [Test]
    public void Decode_rejectsUnsupportedCompression()
    {
        var ihdr = Ihdr(1, 1, 8, 2);
        ihdr[10] = 1;
        var path = Write("compress.png", PngWithChunks(Chunk("IHDR", ihdr), Iend()));
        Assert.That(
            () => ScreenshotPngCodec.DecodeRgba(path, out _, out _),
            Throws.InvalidOperationException.With.Message.Contains("unsupported PNG compression"));
    }

    [Test]
    public void Decode_rejectsNonEightBitRgb()
    {
        var path = Write("depth.png", PngWithChunks(Chunk("IHDR", Ihdr(1, 1, 16, 2)), Iend()));
        Assert.That(
            () => ScreenshotPngCodec.DecodeRgba(path, out _, out _),
            Throws.InvalidOperationException.With.Message.Contains("8-bit RGB"));
    }

    [Test]
    public void Decode_rejectsTruncatedPixelData()
    {
        var path = Write("pixels.png", PngWithChunks(Chunk("IHDR", Ihdr(2, 2, 8, 2)), Chunk("IDAT", Zlib([0, 1, 2, 3])), Iend()));
        Assert.That(
            () => ScreenshotPngCodec.DecodeRgba(path, out _, out _),
            Throws.InvalidOperationException.With.Message.Contains("truncated"));
    }

    [Test]
    public void Decode_rejectsUnsupportedFilter()
    {
        var path = Write("filter.png", EncodeRgb(1, 1, [10, 20, 30], 5));
        Assert.That(
            () => ScreenshotPngCodec.DecodeRgba(path, out _, out _),
            Throws.InvalidOperationException.With.Message.Contains("Unsupported PNG filter"));
    }

    [Test]
    public void Decode_appliesSubUpAverageAndPaethFilters()
    {
        var rgb = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };
        AssertRoundTrip(2, 2, rgb, 1);
        AssertRoundTrip(2, 2, rgb, 2);
        AssertRoundTrip(2, 2, rgb, 3);
        AssertRoundTrip(2, 2, rgb, 4);
    }

    [Test]
    public void Decode_paethPrefersUpLeftWhenItIsClosest()
    {
        var rgb = new byte[] { 10, 0, 0, 20, 0, 0, 0, 0, 0, 5, 5, 5 };
        AssertRoundTrip(2, 2, rgb, 4);
    }

    [Test]
    public void Decode_expandsRgbToOpaqueRgba()
    {
        var path = Write("rgb.png", EncodeRgb(1, 1, [1, 2, 3], 0));
        var pixels = ScreenshotPngCodec.DecodeRgba(path, out var width, out var height);
        Assert.That(width, Is.EqualTo(1));
        Assert.That(height, Is.EqualTo(1));
        Assert.That(pixels, Is.EqualTo(new byte[] { 1, 2, 3, 255 }));
    }

    private static void AssertRoundTrip(int width, int height, byte[] rgb, byte filter)
    {
        var path = Write($"filter-{filter}.png", EncodeRgb(width, height, rgb, filter));
        var pixels = ScreenshotPngCodec.DecodeRgba(path, out var decodedWidth, out var decodedHeight);
        Assert.That(decodedWidth, Is.EqualTo(width));
        Assert.That(decodedHeight, Is.EqualTo(height));
        for (var i = 0; i < rgb.Length; i += 3)
        {
            var dest = i / 3 * 4;
            Assert.That(pixels[dest], Is.EqualTo(rgb[i]));
            Assert.That(pixels[dest + 1], Is.EqualTo(rgb[i + 1]));
            Assert.That(pixels[dest + 2], Is.EqualTo(rgb[i + 2]));
        }
    }

    private static string Write(string name, byte[] bytes)
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-png-codec", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Join(root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] EncodeRgb(int width, int height, byte[] rgb, byte filter)
    {
        const int bytesPerPixel = 3;
        var stride = width * bytesPerPixel;
        var filtered = new byte[height * (1 + stride)];
        var previous = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var row = new byte[stride];
            Buffer.BlockCopy(rgb, y * stride, row, 0, stride);
            var dest = y * (1 + stride);
            filtered[dest] = filter;
            for (var i = 0; i < stride; i++)
            {
                var left = i >= bytesPerPixel ? row[i - bytesPerPixel] : (byte)0;
                var up = previous[i];
                var upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : (byte)0;
                filtered[dest + 1 + i] = filter switch
                {
                    0 => row[i],
                    1 => (byte)(row[i] - left),
                    2 => (byte)(row[i] - up),
                    3 => (byte)(row[i] - ((left + up) / 2)),
                    4 => (byte)(row[i] - Paeth(left, up, upLeft)),
                    _ => row[i]
                };
            }

            previous = row;
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(filtered);
        return PngWithChunks(Chunk("IHDR", Ihdr(width, height, 8, 2)), Chunk("IDAT", compressed.ToArray()), Iend());
    }

    private static byte[] Zlib(byte[] data)
    {
        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(data);
        return compressed.ToArray();
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
            return a;
        return pb <= pc ? b : c;
    }

    private static byte[] PngWithChunks(params byte[][] chunks)
    {
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        foreach (var chunk in chunks)
            png.Write(chunk);
        return png.ToArray();
    }

    private static byte[] Ihdr(int width, int height, byte bitDepth, byte colorType)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), height);
        data[8] = bitDepth;
        data[9] = colorType;
        return data;
    }

    private static byte[] Iend() => Chunk("IEND", []);

    private static byte[] Chunk(string type, byte[] data)
    {
        using var stream = new MemoryStream();
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = 0xFFFFFFFFu;
        foreach (var value in typeBytes.Concat(data))
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        stream.Write(crcBytes);
        return stream.ToArray();
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
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
}
