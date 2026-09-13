using System.Runtime.InteropServices;

namespace AgentUp.Server.Features.DesktopApplications.Models;

[StructLayout(LayoutKind.Sequential)]
internal struct XImageData
{
    public int Width;
    public int Height;
    public int XOffset;
    public int Format;
    public IntPtr Data;
    public int ByteOrder;
    public int BitmapUnit;
    public int BitmapBitOrder;
    public int BitmapPad;
    public int Depth;
    public int BytesPerLine;
    public int BitsPerPixel;
    public nuint RedMask;
    public nuint GreenMask;
    public nuint BlueMask;
}
