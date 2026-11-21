using System.Text.Json;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class ForgeCredentialStore : IForgeCredentialStore
{
    private readonly string _root;

    public ForgeCredentialStore(string dataDirectory)
    {
        _root = Path.GetFullPath(Path.Join(dataDirectory, "forge-credentials"));
    }

    public string? Read(string workspaceId, string moduleId)
    {
        var path = PathFor(workspaceId, moduleId);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    public void Write(string workspaceId, string moduleId, string token)
    {
        WriteSecret(PathFor(workspaceId, moduleId), token);
        // A token written on its own replaces any earlier grant, so a stale refresh token is never paired with it.
        Remove(RefreshPathFor(workspaceId, moduleId));
    }

    public void Delete(string workspaceId, string moduleId)
    {
        Remove(PathFor(workspaceId, moduleId));
        Remove(RefreshPathFor(workspaceId, moduleId));
    }

    public ForgeOAuthGrant? ReadGrant(string workspaceId, string moduleId)
    {
        var token = Read(workspaceId, moduleId);
        if (token is null)
            return null;
        var refreshPath = RefreshPathFor(workspaceId, moduleId);
        if (!File.Exists(refreshPath))
            return new ForgeOAuthGrant(token, null, null);
        try
        {
            var refresh = JsonSerializer.Deserialize<ForgeStoredRefresh>(File.ReadAllText(refreshPath));
            return new ForgeOAuthGrant(token, refresh?.RefreshToken, refresh?.ExpiresAt);
        }
        catch (JsonException)
        {
            return new ForgeOAuthGrant(token, null, null);
        }
    }

    public void WriteGrant(string workspaceId, string moduleId, ForgeOAuthGrant grant)
    {
        WriteSecret(PathFor(workspaceId, moduleId), grant.AccessToken);
        var refreshPath = RefreshPathFor(workspaceId, moduleId);
        if (grant.RefreshToken is null)
            Remove(refreshPath);
        else
            WriteSecret(refreshPath, JsonSerializer.Serialize(new ForgeStoredRefresh(grant.RefreshToken, grant.ExpiresAt)));
    }

    /// <summary>Credentials are readable by the Server's own user only.</summary>
    private static void WriteSecret(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void Remove(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private string RefreshPathFor(string workspaceId, string moduleId) => PathFor(workspaceId, moduleId) + ".refresh";


    private string PathFor(string workspaceId, string moduleId)
    {
        var workspace = RequireSegment(workspaceId, nameof(workspaceId));
        var module = RequireSegment(moduleId, nameof(moduleId));
        var path = Path.GetFullPath(Path.Join(_root, workspace, module));
        if (!IsUnderRoot(_root, path))
            throw new InvalidOperationException("Forge credential path escaped the store root.");
        return path;
    }

    /// <summary>
    /// Workspace and module ids become directory names. Reject traversal and separators before
    /// any filesystem call so a caller cannot point the store outside its root. The value itself
    /// is returned only after those checks, matching the CodeQL single-segment sanitizer.
    /// </summary>
    internal static string RequireSegment(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value is "."
            || Path.GetFileName(value) != value
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException($"Forge credential {name} is not a safe path segment.");
        }

        return value;
    }

    internal static bool IsUnderRoot(string root, string path)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal);
    }
}
