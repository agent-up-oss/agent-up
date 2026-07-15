using Avalonia.Controls;
using Avalonia.Threading;

namespace AgentUp.InstallerApp.Tests.Support;

internal static class HeadlessExtensions
{
    public static async Task FlushAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Task.Delay(10);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

    public static T Find<T>(this Control root, string name) where T : Control
        => root.FindControl<T>(name) ?? throw new InvalidOperationException($"Could not find {name}.");
}
