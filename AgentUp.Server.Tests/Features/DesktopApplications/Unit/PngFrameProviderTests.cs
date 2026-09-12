using System.Buffers.Binary;
using System.IO.Compression;
using AgentUp.Server.Features.DesktopApplications.Providers;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Unit;

[TestFixture]
public sealed class PngFrameProviderTests
{
    [Test]
    public void Encodes_a_valid_rgb_png_with_expected_dimensions_and_pixels()
    {
        var png = new PngFrameProvider().EncodeRgb(2, 1, [255, 0, 0, 0, 255, 0]);

        Assert.That(png.Take(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        Assert.That(BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), Is.EqualTo(2));
        Assert.That(BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)), Is.EqualTo(1));

        var idatLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33, 4));
        using var compressed = new MemoryStream(png, 41, idatLength);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        Assert.That(raw.ToArray(), Is.EqualTo(new byte[] { 0, 255, 0, 0, 0, 255, 0 }));
    }
}
