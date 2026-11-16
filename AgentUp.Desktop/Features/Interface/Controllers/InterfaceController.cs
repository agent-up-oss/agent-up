using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Controllers;

/// <summary>
/// The switch between the two shells. Both sides can reach it: the classic shell turns the
/// command center on, the command center turns it off, so neither can strand the user.
/// </summary>
public sealed class InterfaceController(IInterfacePreferenceStore store)
{
    private InterfacePreference _preference = store.Load();

    public InterfaceChoice Choice => _preference.Choice;

    public bool UsesCommandCenter => _preference.Choice == InterfaceChoice.CommandCenter;

    /// <summary>Applies the choice and reports whether it was stored for the next launch.</summary>
    public bool Choose(InterfaceChoice choice)
    {
        _preference = _preference with { Choice = choice };
        return store.Save(_preference);
    }

    public bool Toggle() =>
        Choose(UsesCommandCenter ? InterfaceChoice.Classic : InterfaceChoice.CommandCenter);
}
