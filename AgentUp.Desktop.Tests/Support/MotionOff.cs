using System.Runtime.CompilerServices;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Headless tests assert where the interface ends up, and nothing drives a render clock between
/// their steps, so the whole test assembly runs with motion off. Motion is tested directly.
/// </summary>
internal static class MotionOff
{
    [ModuleInitializer]
    internal static void TurnMotionOff() => MotionProvider.Enabled = false;
}
