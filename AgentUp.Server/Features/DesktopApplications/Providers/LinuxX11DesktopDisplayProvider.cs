using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AgentUp.Server.Features.DesktopApplications.Interfaces;
using AgentUp.Server.Features.DesktopApplications.Models;

namespace AgentUp.Server.Features.DesktopApplications.Providers;

public sealed class LinuxX11DesktopDisplayProvider(PngFrameProvider pngFrames) : IDesktopDisplayProvider
{
    public async Task<DesktopDisplayHandle> StartAsync(int width, int height, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("Desktop applications currently require a Linux Agent-Up Server host.");
        if (width is < 320 or > 3840 || height is < 240 or > 2160)
            throw new InvalidOperationException("Desktop window dimensions must be between 320x240 and 3840x2160.");

        var displayName = $":{RandomNumberGenerator.GetInt32(100, 60_000)}";
        var process = CreateDisplayProcess(displayName, width, height);

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            throw new InvalidOperationException("Desktop hosting requires Xvfb on the Server host.", ex);
        }

        var handle = new DesktopDisplayHandle(displayName, process, width, height);
        try
        {
            await WaitUntilReadyAsync(handle, cancellationToken);
            return handle;
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            await StopAsync(handle, CancellationToken.None);
            throw;
        }
    }

    private static Process CreateDisplayProcess(string displayName, int width, int height)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "Xvfb",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        process.StartInfo.ArgumentList.Add(displayName);
        process.StartInfo.ArgumentList.Add("-screen");
        process.StartInfo.ArgumentList.Add("0");
        process.StartInfo.ArgumentList.Add($"{width}x{height}x24");
        process.StartInfo.ArgumentList.Add("-nolisten");
        process.StartInfo.ArgumentList.Add("tcp");

        return process;
    }

    public Task<byte[]> CapturePngAsync(DesktopDisplayHandle display, CancellationToken cancellationToken) =>
        Task.Run(() => Capture(display, cancellationToken), cancellationToken);

    public Task SendPointerAsync(
        DesktopDisplayHandle display,
        int x,
        int y,
        int button,
        bool pressed,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (x < 0 || x >= display.Width || y < 0 || y >= display.Height)
            throw new InvalidOperationException("Pointer coordinates are outside the desktop framebuffer.");

        WithDisplay(display.DisplayName, connection =>
        {
            XTestFakeMotionEvent(connection, -1, x, y, 0);
            if (button >= 0)
                XTestFakeButtonEvent(connection, (uint)(button + 1), pressed, 0);
            XFlush(connection);
        });
        return Task.CompletedTask;
    }

    public Task SendKeyAsync(DesktopDisplayHandle display, string key, bool pressed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 32)
            throw new InvalidOperationException("Desktop key must be a supported X11 key name.");

        WithDisplay(display.DisplayName, connection =>
        {
            var symbol = XStringToKeysym(NormalizeKey(key));
            var keyCode = symbol == 0 ? (byte)0 : XKeysymToKeycode(connection, symbol);
            if (keyCode == 0)
                throw new InvalidOperationException($"Desktop key '{key}' is not supported.");
            XTestFakeKeyEvent(connection, keyCode, pressed, 0);
            XFlush(connection);
        });
        return Task.CompletedTask;
    }

    public async Task StopAsync(DesktopDisplayHandle display, CancellationToken cancellationToken)
    {
        if (!display.DisplayProcess.HasExited)
        {
            display.DisplayProcess.Kill(entireProcessTree: true);
            await display.DisplayProcess.WaitForExitAsync(cancellationToken);
        }
        display.DisplayProcess.Dispose();
    }

    private async Task WaitUntilReadyAsync(DesktopDisplayHandle display, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (display.DisplayProcess.HasExited)
                throw new InvalidOperationException("Xvfb exited before the desktop display became ready.");
            var connection = XOpenDisplay(display.DisplayName);
            if (connection != IntPtr.Zero)
            {
                XCloseDisplay(connection);
                return;
            }
            await Task.Delay(50, cancellationToken);
        }
        throw new InvalidOperationException("Timed out waiting for the desktop display.");
    }

    private byte[] Capture(DesktopDisplayHandle display, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? png = null;
        WithDisplay(display.DisplayName, connection =>
        {
            var root = XDefaultRootWindow(connection);
            var imagePointer = XGetImage(connection, root, 0, 0, (uint)display.Width, (uint)display.Height, nuint.MaxValue, 2);
            if (imagePointer == IntPtr.Zero)
                throw new InvalidOperationException("Could not capture the desktop framebuffer.");
            try
            {
                var image = Marshal.PtrToStructure<XImageData>(imagePointer);
                if (image.BitsPerPixel is not (24 or 32))
                    throw new InvalidOperationException($"Unsupported X11 framebuffer depth: {image.BitsPerPixel} bits per pixel.");
                var source = new byte[checked(image.BytesPerLine * display.Height)];
                Marshal.Copy(image.Data, source, 0, source.Length);
                var rgb = new byte[checked(display.Width * display.Height * 3)];
                for (var y = 0; y < display.Height; y++)
                for (var x = 0; x < display.Width; x++)
                {
                    var pixel = ReadPixel(source, image, x, y);
                    var offset = (y * display.Width + x) * 3;
                    rgb[offset] = ScaleMask(pixel, image.RedMask);
                    rgb[offset + 1] = ScaleMask(pixel, image.GreenMask);
                    rgb[offset + 2] = ScaleMask(pixel, image.BlueMask);
                }
                png = pngFrames.EncodeRgb(display.Width, display.Height, rgb);
            }
            finally
            {
                XDestroyImage(imagePointer);
            }
        });
        return png ?? throw new InvalidOperationException("Could not capture the desktop framebuffer.");
    }

    private static nuint ReadPixel(byte[] source, XImageData image, int x, int y)
    {
        var bytesPerPixel = image.BitsPerPixel / 8;
        var offset = checked(y * image.BytesPerLine + x * bytesPerPixel);
        nuint pixel = 0;
        if (image.ByteOrder == 0)
        {
            for (var index = bytesPerPixel - 1; index >= 0; index--)
                pixel = (pixel << 8) | source[offset + index];
        }
        else
        {
            for (var index = 0; index < bytesPerPixel; index++)
                pixel = (pixel << 8) | source[offset + index];
        }
        return pixel;
    }

    private static byte ScaleMask(nuint pixel, nuint mask)
    {
        if (mask == 0) return 0;
        var shift = 0;
        var shiftedMask = mask;
        while ((shiftedMask & 1) == 0) { shiftedMask >>= 1; shift++; }
        var value = (pixel & mask) >> shift;
        return (byte)(value * 255 / shiftedMask);
    }

    private static void WithDisplay(string displayName, Action<IntPtr> action)
    {
        var connection = XOpenDisplay(displayName);
        if (connection == IntPtr.Zero)
            throw new InvalidOperationException("Desktop display is unavailable.");
        try { action(connection); }
        finally { XCloseDisplay(connection); }
    }

    private static string NormalizeKey(string key) => key switch
    {
        " " => "space",
        "ArrowUp" => "Up",
        "ArrowDown" => "Down",
        "ArrowLeft" => "Left",
        "ArrowRight" => "Right",
        _ => key
    };

    [DllImport("libX11.so.6", CharSet = CharSet.Ansi)] private static extern IntPtr XOpenDisplay(string displayName);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] private static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, nuint planeMask, int format);
    [DllImport("libX11.so.6")] private static extern int XDestroyImage(IntPtr image);
    [DllImport("libX11.so.6")] private static extern int XFlush(IntPtr display);
    [DllImport("libX11.so.6", CharSet = CharSet.Ansi)] private static extern nuint XStringToKeysym(string name);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(IntPtr display, nuint keysym);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeMotionEvent(IntPtr display, int screen, int x, int y, ulong delay);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(IntPtr display, uint button, [MarshalAs(UnmanagedType.Bool)] bool isPress, ulong delay);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeKeyEvent(IntPtr display, uint keycode, [MarshalAs(UnmanagedType.Bool)] bool isPress, ulong delay);
}
