using System.Text.Json;
using AgentUp.Server.Features.TaskSource.Interfaces;

namespace AgentUp.Server.Features.TaskSource.Providers;

/// <summary>
/// The task source's token on disk, by module.
///
/// The file is written for this process's own user only. It holds a credential that can read a
/// repository, so it is not left at whatever the directory's default happens to be.
/// </summary>
public sealed class JsonTaskSourceCredentialStore : ITaskSourceCredentialStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly object _gate = new();

    public JsonTaskSourceCredentialStore(string dataDirectory)
        => _path = Path.Join(dataDirectory, "task-source-credentials.json");

    public string? Read(string moduleId)
    {
        lock (_gate)
            return Tokens().TryGetValue(moduleId, out var token) ? token : null;
    }

    public void Write(string moduleId, string token)
    {
        lock (_gate)
        {
            var tokens = Tokens();
            tokens[moduleId] = token;
            Persist(tokens);
        }
    }

    public void Delete(string moduleId)
    {
        lock (_gate)
        {
            var tokens = Tokens();
            if (tokens.Remove(moduleId)) Persist(tokens);
        }
    }

    private Dictionary<string, string> Tokens()
    {
        if (!File.Exists(_path))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path), Json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist(Dictionary<string, string> tokens)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(tokens, Json));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
