using System.Buffers.Binary;
using System.IO.Compression;

namespace AgentUp.Server.Features.DesktopApplications.Providers;

public sealed class PngFrameProvider
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public byte[] EncodeRgb(int width, int height, ReadOnlySpan<byte> rgb)
    {
        if (width <= 0 || height <= 0 || rgb.Length != checked(width * height * 3))
            throw new ArgumentException("RGB frame dimensions do not match its payload.", nameof(rgb));

        using var output = new MemoryStream();
        output.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(output, "IHDR"u8, header);

        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgb.Slice(y * width * 3, width * 3));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(raw.ToArray());
        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        output.Write(length);
        output.Write(type);
        output.Write(payload);
        var crcInput = new byte[type.Length + payload.Length];
        type.CopyTo(crcInput);
        payload.CopyTo(crcInput.AsSpan(type.Length));
        BinaryPrimitives.WriteUInt32BigEndian(length, Crc32(crcInput));
        output.Write(length);
    }

    private static uint Crc32(ReadOnlySpan<byte> value)
    {
        var crc = uint.MaxValue;
        foreach (var current in value)
        {
            crc ^= current;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }
}
