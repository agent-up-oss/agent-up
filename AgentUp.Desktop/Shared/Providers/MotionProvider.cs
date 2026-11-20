namespace AgentUp.Desktop.Shared.Providers;

/// <summary>
/// The timing behind the Desktop's motion: one switch, and the pure easing steps the views run
/// once per frame. Every step is time-based, so a slow frame moves further rather than slower.
/// </summary>
public static class MotionProvider
{
    /// <summary>
    /// Whether the Desktop animates at all. Off, every change lands at once: that is what reduced
    /// motion asks for (<c>AGENTUP_REDUCED_MOTION=1</c>), and what a headless test needs to assert
    /// a final state without driving a render clock.
    /// </summary>
    public static bool Enabled { get; set; } = Environment.GetEnvironmentVariable("AGENTUP_REDUCED_MOTION") is not ("1" or "true");

    /// <summary>How long expand, collapse and arrival take.</summary>
    public static readonly TimeSpan Reveal = TimeSpan.FromMilliseconds(220);

    /// <summary>Time constant of the scroll follow: about two thirds of the way in this long.</summary>
    public const double FollowTimeConstantMs = 90;

    /// <summary>
    /// The next scroll offset on the way to <paramref name="target"/>: exponential smoothing that
    /// starts fast and settles without overshoot, and snaps the last half pixel.
    /// </summary>
    public static double FollowStep(double current, double target, double elapsedMs)
    {
        var next = current + (target - current) * (1 - Math.Exp(-Math.Max(0, elapsedMs) / FollowTimeConstantMs));
        return Math.Abs(target - next) < 0.5 ? target : next;
    }

    /// <summary>
    /// How much of a streamed text to show next. Text arrives in bursts; showing it at a pace that
    /// catches up within about a quarter of a second makes it flow instead of pop, without ever
    /// falling far behind the agent.
    /// </summary>
    public static int RevealStep(int shown, int target, double elapsedMs)
    {
        if (shown >= target)
            return target;
        var remaining = target - shown;
        var frames = Math.Max(1, elapsedMs / 16.0);
        var step = (int)Math.Ceiling(Math.Max(4, remaining * 0.25) * frames);
        return Math.Min(target, shown + step);
    }
}
