using System.Windows.Input;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels.Chrome;

public sealed class ChromeIconButtonViewModel : ReactiveObject
{
    public ChromeIconButtonViewModel(
        string name,
        string icon,
        double fontSize,
        ICommand command,
        string toolTip,
        string automationId)
    {
        Name = name;
        Icon = icon;
        FontSize = fontSize;
        Command = command;
        ToolTip = toolTip;
        AutomationId = automationId;
    }

    public string Name { get; }

    public string AutomationId { get; }

    public string Icon { get; }

    public double FontSize { get; }

    public ICommand Command { get; }

    public string ToolTip { get; }
}
