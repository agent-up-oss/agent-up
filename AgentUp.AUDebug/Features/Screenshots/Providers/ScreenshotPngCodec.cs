using System.Buffers.Binary;
using System.IO.Compression;

namespace AgentUp.AUDebug.Features.Screenshots.Providers;

public static class ScreenshotPngCodec
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] DecodeRgba(string path, out int width, out int height)
    {
        using var stream = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[8];
        ReadExact(stream, signature);
        if (!signature.SequenceEqual(Signature))
            throw new InvalidOperationException($"'{path}' is not a PNG screenshot.");

        width = 0;
        height = 0;
        var bitDepth = 0;
        var colorType = 0;
        using var idat = new MemoryStream();
        Span<byte> lengthBytes = stackalloc byte[4];
        Span<byte> typeBytes = stackalloc byte[4];
        while (stream.Position < stream.Length)
        {
            ReadExact(stream, lengthBytes);
            var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
            ReadExact(stream, typeBytes);
            var type = System.Text.Encoding.ASCII.GetString(typeBytes);
            var data = new byte[length];
            ReadExact(stream, data);
            stream.Position += 4;
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                bitDepth = data[8];
                colorType = data[9];
                if (data[10] != 0 || data[11] != 0 || data[12] != 0)
                    throw new InvalidOperationException($"'{path}' uses an unsupported PNG compression or filter.");
            }
            else if (type == "IDAT")
            {
                idat.Write(data);
            }
            else if (type == "IEND")
            {
                break;
            }
        }

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException($"'{path}' is missing a PNG header.");
        if (bitDepth != 8 || colorType is not (2 or 6))
            throw new InvalidOperationException($"'{path}' is not an 8-bit RGB or RGBA PNG.");

        idat.Position = 0;
        using var deflate = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        deflate.CopyTo(raw);
        return Unfilter(raw.ToArray(), width, height, colorType == 6 ? 4 : 3);
    }

    public static byte[] EncodeRgb(int width, int height, byte[] rgb)
    {
        var stride = width * 3;
        var filtered = new byte[height * (1 + stride)];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + stride);
            filtered[row] = 0;
            Buffer.BlockCopy(rgb, y * stride, filtered, row + 1, stride);
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(filtered);
        var idat = compressed.ToArray();
        using var png = new MemoryStream();
        png.Write(Signature);
        WriteChunk(png, "IHDR", Ihdr(width, height));
        WriteChunk(png, "IDAT", idat);
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static byte[] Unfilter(byte[] data, int width, int height, int bytesPerPixel)
    {
        var stride = width * bytesPerPixel;
        var rgba = new byte[width * height * 4];
        var previous = new byte[stride];
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            if (offset >= data.Length)
                throw new InvalidOperationException("PNG pixel data is truncated.");
            var filter = data[offset++];
            var row = new byte[stride];
            if (offset + stride > data.Length)
                throw new InvalidOperationException("PNG pixel data is truncated.");
            Buffer.BlockCopy(data, offset, row, 0, stride);
            offset += stride;
            ApplyFilter(filter, row, previous, bytesPerPixel);
            for (var x = 0; x < width; x++)
            {
                var src = x * bytesPerPixel;
                var dest = (y * width + x) * 4;
                rgba[dest] = row[src];
                rgba[dest + 1] = row[src + 1];
                rgba[dest + 2] = row[src + 2];
                rgba[dest + 3] = bytesPerPixel == 4 ? row[src + 3] : (byte)255;
            }

            previous = row;
        }

        return rgba;
    }

    private static void ApplyFilter(byte filter, byte[] row, byte[] previous, int bytesPerPixel)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bytesPerPixel ? row[i - bytesPerPixel] : (byte)0;
            var up = previous[i];
            var upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : (byte)0;
            row[i] = filter switch
            {
                0 => row[i],
                1 => (byte)(row[i] + left),
                2 => (byte)(row[i] + up),
                3 => (byte)(row[i] + ((left + up) / 2)),
                4 => (byte)(row[i] + Paeth(left, up, upLeft)),
                _ => throw new InvalidOperationException($"Unsupported PNG filter '{filter}'.")
            };
        }
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

    private static byte[] Ihdr(int width, int height)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), height);
        data[8] = 8;
        data[9] = 2;
        return data;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var value in type)
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        foreach (var value in data)
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private static void ReadExact(Stream stream, Span<byte> buffer)
    {
        var read = stream.Read(buffer);
        if (read != buffer.Length)
            throw new InvalidOperationException("PNG file is truncated.");
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
