namespace AgentUp.Browser.Streaming.Models;

public enum ControlAuthority { Human, Ai }

public sealed record BrowserControlMode(ControlAuthority Authority, int Width, int Height)
{
    public static BrowserControlMode DefaultAi =>
        new(ControlAuthority.Ai, BrowserViewportPreset.Default.Width, BrowserViewportPreset.Default.Height);

    public static BrowserControlMode DefaultHuman => new(ControlAuthority.Human, 0, 0);
}
