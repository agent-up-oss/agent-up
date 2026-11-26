using System.Text.Json;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;

namespace AgentUp.Server.Features.TaskSource.Providers;

/// <summary>
/// The task source's tokens on disk, keyed by team member. A legacy file keyed by host is still
/// readable.
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

    public IReadOnlyList<string> Keys()
    {
        lock (_gate)
            return Credentials().Keys.ToArray();
    }

    public TaskSourceCredential? Read(string key)
    {
        lock (_gate)
            return Credentials().TryGetValue(key, out var credential) ? credential : null;
    }

    public void Write(string key, TaskSourceCredential credential)
    {
        lock (_gate)
        {
            var credentials = Credentials();
            credentials[key] = credential;
            Persist(credentials);
        }
    }

    public void Delete(string key)
    {
        lock (_gate)
        {
            var credentials = Credentials();
            if (credentials.Remove(key)) Persist(credentials);
        }
    }

    private Dictionary<string, TaskSourceCredential> Credentials()
    {
        if (!File.Exists(_path))
            return new Dictionary<string, TaskSourceCredential>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            return document.RootElement.EnumerateObject()
                .Select(property => (property.Name, Credential: Parse(property.Name, property.Value)))
                .Where(entry => entry.Credential is not null)
                .ToDictionary(
                    entry => entry.Name,
                    entry => entry.Credential!,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, TaskSourceCredential>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static TaskSourceCredential? Parse(string key, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var token = value.GetString();
            return string.IsNullOrWhiteSpace(token) ? null : new TaskSourceCredential(token, "", HostKey(key));
        }

        if (value.ValueKind != JsonValueKind.Object)
            return null;
        var stored = value.TryGetProperty("token", out var tokenProperty) ? tokenProperty.GetString() : null;
        if (string.IsNullOrWhiteSpace(stored))
            return null;
        var endpoint = value.TryGetProperty("endpointUrl", out var endpointProperty)
            ? endpointProperty.GetString() ?? ""
            : "";
        var module = value.TryGetProperty("moduleId", out var moduleProperty)
            ? moduleProperty.GetString() ?? ""
            : "";
        if (string.IsNullOrWhiteSpace(module))
            module = HostKey(key);
        return new TaskSourceCredential(stored, endpoint, module);
    }

    private static string HostKey(string key)
        => key.Equals("github", StringComparison.OrdinalIgnoreCase)
            || key.Equals("forgejo", StringComparison.OrdinalIgnoreCase)
            ? key.ToLowerInvariant()
            : "";

    private void Persist(Dictionary<string, TaskSourceCredential> credentials)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(credentials, Json));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
