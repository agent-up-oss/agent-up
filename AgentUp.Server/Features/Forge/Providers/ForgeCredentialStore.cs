using AgentUp.Server.Features.Forge.Interfaces;

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
        var path = PathFor(workspaceId, moduleId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, token);
    }

    public void Delete(string workspaceId, string moduleId)
    {
        var path = PathFor(workspaceId, moduleId);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string PathFor(string workspaceId, string moduleId)
    {
        var encoded = Uri.EscapeDataString(workspaceId);
        var safeModule = Uri.EscapeDataString(moduleId);
        var path = Path.GetFullPath(Path.Join(_root, encoded, safeModule));
        if (!path.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("Forge credential path escaped the store root.");
        return path;
    }
}
