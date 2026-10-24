using System.Text.Json;
using System.Security.Cryptography;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class GithubAppConnectionStore : IGithubAppConnectionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _root;
    private readonly object _gate = new();

    public GithubAppConnectionStore(string dataDirectory)
        => _root = Path.GetFullPath(Path.Join(dataDirectory, "forge-apps"));

    public GithubAppConnection? Read(string workspaceId)
    {
        lock (_gate)
        {
            var path = PathFor(workspaceId);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<GithubAppConnection>(File.ReadAllText(path), Json)
                : null;
        }
    }

    public GithubAppConnection? FindByState(string state)
        => ReadAll().FirstOrDefault(item => SecretEquals(item.State, state));

    public GithubAppConnection? FindBySetupKey(string setupKey)
        => ReadAll().FirstOrDefault(item => SecretEquals(item.SetupKey, setupKey));

    public void Write(GithubAppConnection connection)
    {
        lock (_gate)
        {
            var path = PathFor(connection.WorkspaceId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(connection, Json));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, path, true);
        }
    }

    public void Delete(string workspaceId)
    {
        lock (_gate)
        {
            var path = PathFor(workspaceId);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private IReadOnlyList<GithubAppConnection> ReadAll()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_root)) return [];
            return Directory.EnumerateFiles(_root, "*.json")
                .Select(path => JsonSerializer.Deserialize<GithubAppConnection>(File.ReadAllText(path), Json))
                .Where(item => item is not null).Cast<GithubAppConnection>().ToArray();
        }
    }

    private string PathFor(string workspaceId)
        => Path.Join(_root, ForgeCredentialStore.RequireSegment(workspaceId, nameof(workspaceId)) + ".json");

    private static bool SecretEquals(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual)) return false;
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        var actualBytes = System.Text.Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
