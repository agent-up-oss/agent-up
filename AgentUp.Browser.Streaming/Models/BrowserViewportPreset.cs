namespace AgentUp.Browser.Streaming.Models;

public sealed record BrowserViewportPreset(string Id, string Label, int Width, int Height)
{
    public static readonly BrowserViewportPreset[] Standard =
    [
        new("mobile", "Mobile", 375, 667),
        new("tablet", "Tablet", 768, 1024),
        new("desktop", "Desktop", 1280, 720),
        new("wide", "Wide", 1440, 900),
        new("full-hd", "Full HD", 1920, 1080)
    ];

    public static BrowserViewportPreset Default => Standard[2];

    public static BrowserViewportPreset? Find(string id)
        => Standard.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public static BrowserViewportPreset? Find(int width, int height)
        => Standard.FirstOrDefault(p => p.Width == width && p.Height == height);
}
