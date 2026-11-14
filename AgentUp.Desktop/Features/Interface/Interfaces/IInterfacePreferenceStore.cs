using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Interfaces;

public interface IInterfacePreferenceStore
{
    InterfacePreference Load();

    /// <summary>Returns false when the choice could not be written, so a caller can say so.</summary>
    bool Save(InterfacePreference preference);
}
