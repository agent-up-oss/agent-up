namespace AgentUp.Tray.Features.AutoStart;

public interface IAutoStartRegistrar
{
    bool IsRegistered();
    void Register();
    void Unregister();

    void EnsureRegistered()
    {
        if (!IsRegistered()) Register();
    }
}
