using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace AgentUp.Desktop.Features.Validation.Providers;

public sealed class DesktopWindowCapture
{
    public void CapturePng(TopLevel topLevel, string path)
    {
        var width = Math.Max(1, (int)Math.Ceiling(Math.Max(topLevel.Bounds.Width, 1) * Math.Max(topLevel.RenderScaling, 1)));
        var height = Math.Max(1, (int)Math.Ceiling(Math.Max(topLevel.Bounds.Height, 1) * Math.Max(topLevel.RenderScaling, 1)));
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        if (topLevel is Visual visual)
            bitmap.Render(visual);
        bitmap.Save(path);
    }
}
