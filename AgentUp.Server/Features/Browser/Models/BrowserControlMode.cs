namespace AgentUp.Server.Features.Browser.Models;

public enum ControlAuthority { Human, Ai }

public sealed record BrowserControlMode(ControlAuthority Authority, int Width, int Height)
{
    public static readonly int[] AllowedWidths  = [375, 768, 1024, 1280, 1920];
    public static readonly int[] AllowedHeights = [667, 720, 768, 900, 1080];
    public static BrowserControlMode DefaultAi    => new(ControlAuthority.Ai,    1280, 720);
    public static BrowserControlMode DefaultHuman => new(ControlAuthority.Human, 0, 0);
}
