using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AgentUp.InstallerApp.Features.Installation.Views;

public partial class InstallerWindow : Window
{
    public InstallerWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
