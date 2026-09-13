using System.Text.Json;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Host.Providers;

public sealed class HostSessionStore : IHostSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IDebugPathValidator _paths;

    public HostSessionStore(IDebugPathValidator paths) => _paths = paths;

    public HostSessionDto? Read()
    {
        var path = SessionFile();
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<HostSessionDto>(json, JsonOptions);
    }

    public void Write(HostSessionDto session)
    {
        Directory.CreateDirectory(_paths.SessionDirectory);
        Directory.CreateDirectory(_paths.LogsDirectory);
        Directory.CreateDirectory(_paths.ScreenshotsDirectory);
        File.WriteAllText(SessionFile(), JsonSerializer.Serialize(session, JsonOptions));
    }

    public void Delete()
    {
        var path = SessionFile();
        if (File.Exists(path))
            File.Delete(path);
    }

    public string ScreenshotPath(string surface)
    {
        var safeSurface = surface is "desktop" or "mobile" or "docs" ? surface : "surface";
        Directory.CreateDirectory(_paths.ScreenshotsDirectory);
        var name = $"{safeSurface}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.png";
        return _paths.EnsureUnderRoot(Path.Join(_paths.ScreenshotsDirectory, name));
    }

    public string ReadLogTail(string logPath, int lineCount)
    {
        var path = _paths.EnsureUnderRoot(logPath);
        if (!File.Exists(path))
            return string.Empty;

        var lines = File.ReadAllLines(path);
        var start = Math.Max(0, lines.Length - lineCount);
        return string.Join(Environment.NewLine, lines[start..]);
    }

    private string SessionFile()
        => _paths.EnsureUnderRoot(Path.Join(_paths.SessionDirectory, "session.json"));
}
