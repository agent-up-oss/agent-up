using Avalonia.Input;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Console.Unit;

[TestFixture]
public sealed class ConsoleKeyboardInputTests
{
    [Test]
    public void IsInterruptKey_UsesControlC_OnLinux()
    {
        Assert.That(ConsoleKeyboardInputProvider.IsInterruptKey(Key.C, KeyModifiers.Control), Is.True);
        Assert.That(ConsoleKeyboardInputProvider.IsInterruptKey(Key.C, KeyModifiers.Meta), Is.False);
    }

    [Test]
    public void IsInterruptKey_UsesControlC_OnMac()
    {
        Assert.That(ConsoleKeyboardInputProvider.IsInterruptKey(Key.C, KeyModifiers.Control), Is.True);
        Assert.That(ConsoleKeyboardInputProvider.IsInterruptKey(Key.C, KeyModifiers.Meta), Is.False);
    }

    [Test]
    public void IsCopyKey_UsesMetaC_OnMac()
    {
        Assert.That(ConsoleKeyboardInputProvider.IsCopyKey(Key.C, KeyModifiers.Meta, isMac: true), Is.True);
        Assert.That(ConsoleKeyboardInputProvider.IsCopyKey(Key.C, KeyModifiers.Control, isMac: true), Is.False);
        Assert.That(ConsoleKeyboardInputProvider.IsCopyKey(Key.C, KeyModifiers.Control | KeyModifiers.Meta, isMac: true), Is.False);
    }

    [Test]
    public void IsCopyKey_UsesControlC_OnLinux()
    {
        Assert.That(ConsoleKeyboardInputProvider.IsCopyKey(Key.C, KeyModifiers.Control, isMac: false), Is.True);
        Assert.That(ConsoleKeyboardInputProvider.IsCopyKey(Key.C, KeyModifiers.Meta, isMac: false), Is.False);
    }
}
