using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace AgentUp.Desktop.Tests.Support;

internal static class HeadlessExtensions
{
    public static async Task ClickControlAsync(this TopLevel topLevel, Control control)
    {
        var transform = control.TransformToVisual(topLevel);
        if (transform is null)
            throw new InvalidOperationException($"Control '{control.Name}' is not attached to the visual tree.");

        var bounds = control.Bounds;
        var center = transform.Value.Transform(new Point(bounds.Width / 2, bounds.Height / 2));

        topLevel.MouseMove(center);
        topLevel.MouseDown(center, MouseButton.Left);
        topLevel.MouseUp(center, MouseButton.Left);

        await FlushAsync();
    }

    public static async Task FlushAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }
}
