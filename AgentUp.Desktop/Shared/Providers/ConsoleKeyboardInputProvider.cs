using Avalonia.Input;

namespace AgentUp.Desktop.Shared.Providers;

public static class ConsoleKeyboardInputProvider
{
    public static bool IsInterruptKey(Key key, KeyModifiers modifiers)
        => key == Key.C && modifiers.HasFlag(KeyModifiers.Control);

    public static bool IsCopyKey(Key key, KeyModifiers modifiers, bool isMac)
        => key == Key.C
           && modifiers.HasFlag(isMac ? KeyModifiers.Meta : KeyModifiers.Control)
           && (!isMac || !modifiers.HasFlag(KeyModifiers.Control));
}
