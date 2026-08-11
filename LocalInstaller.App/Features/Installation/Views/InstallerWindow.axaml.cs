using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.InstallerApp.Features.Logging.Tools;

namespace AgentUp.InstallerApp.Features.Installation.Views;

public partial class InstallerWindow : Window
{
    public InstallerWindow()
    {
        AvaloniaXamlLoader.Load(this);
        SetWindowIcon();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is not InstallerViewModel model)
            return;

        try
        {
            await model.RefreshAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            InstallerLog.WriteException("window refresh", ex);
        }
    }

    private void SetWindowIcon()
    {
        try
        {
            var iconPath = FindWindowIconPath();
            if (iconPath is null) return;

            using var stream = File.OpenRead(iconPath);
            Icon = new WindowIcon(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            InstallerLog.WriteException("window icon", ex);
            // Window icons are best-effort and should never block InstallerApp startup.
        }
    }

    internal static string? FindWindowIconPath()
    {
        var outputPath = Path.Join(AppContext.BaseDirectory, "media", "logo.png");
        if (File.Exists(outputPath)) return outputPath;

        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            var candidate = Path.Join(dir, "media", "logo.png");
            if (File.Exists(candidate)) return candidate;

            var parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (parent == dir) break;
            dir = parent;
        }

        return null;
    }
}
