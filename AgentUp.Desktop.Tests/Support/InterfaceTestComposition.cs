using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Keeps the suite off the real interface preference file. Production composition falls back to
/// <c>FileInterfacePreferenceStore</c>, so a test that builds the shell without saying which one it
/// wants would read whichever shell the developer last chose on this machine, and assert against a
/// window it never asked for.
/// </summary>
internal static class InterfaceTestComposition
{
    /// <summary>The shell every test gets unless it says otherwise: the classic one.</summary>
    public static CommandCenterViewModel Classic(BridgeViewModel? bridge = null)
        => new(new InterfaceController(new MemoryInterfacePreferenceStore()), bridge);

    public static CommandCenterViewModel CommandCenter(BridgeViewModel? bridge = null)
    {
        var controller = new InterfaceController(new MemoryInterfacePreferenceStore());
        controller.Choose(InterfaceChoice.CommandCenter);
        return new CommandCenterViewModel(controller, bridge);
    }
}

internal sealed class MemoryInterfacePreferenceStore : IInterfacePreferenceStore
{
    private InterfacePreference _preference = new();

    public InterfacePreference Load() => _preference;

    public bool Save(InterfacePreference preference)
    {
        _preference = preference;
        return true;
    }
}
