using System.Text.Json;
using AgentUp.Desktop.Features.Authentication.Interfaces;
using AgentUp.Desktop.Features.Authentication.Models;

namespace AgentUp.Desktop.Features.Authentication.Providers;

public sealed class FileServerConnectionStore : IServerConnectionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public FileServerConnectionStore()
        : this(Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Agent-Up",
            "connections.json"))
    {
    }

    public FileServerConnectionStore(string filePath)
    {
        _filePath = filePath;
    }

    public ServerSelection Load()
    {
        if (!File.Exists(_filePath))
            return new ServerSelection();

        try
        {
            var json = File.ReadAllText(_filePath);
            return Normalize(JsonSerializer.Deserialize<ServerSelection>(json, Options) ?? new ServerSelection());
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new ServerSelection();
        }
    }

    public void Save(ServerSelection selection)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Connection store path must include a directory.");

        Directory.CreateDirectory(directory);
        RestrictDirectoryAccess(directory);
        var json = JsonSerializer.Serialize(Normalize(selection), Options);
        var tempPath = Path.Join(directory, $"{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            WriteOwnerOnlyText(tempPath, json);
            File.Move(tempPath, _filePath, true);
            RestrictFileAccess(_filePath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static ServerSelection Normalize(ServerSelection selection)
    {
        var servers = selection.Servers
            .Where(server => !string.IsNullOrWhiteSpace(server.Id) && !string.IsNullOrWhiteSpace(server.Url))
            .ToList();
        var activeServerId = servers.Any(server => server.Id == selection.ActiveServerId)
            ? selection.ActiveServerId
            : servers.FirstOrDefault()?.Id;
        return new ServerSelection { Servers = servers, ActiveServerId = activeServerId };
    }

    private static void WriteOwnerOnlyText(string path, string contents)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, contents);
            return;
        }

        using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
            });
        using var writer = new StreamWriter(stream);
        writer.Write(contents);
    }

    private static void RestrictDirectoryAccess(string directory)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void RestrictFileAccess(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
