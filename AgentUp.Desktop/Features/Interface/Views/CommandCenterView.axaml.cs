using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AgentUp.Desktop.Features.Interface.Views;

public partial class CommandCenterView : UserControl
{
    public CommandCenterView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
