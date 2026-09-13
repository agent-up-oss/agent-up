using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentClaudeCredentialStore : IAgentClaudeCredentialStore
{
    private readonly string _path;

    public AgentClaudeCredentialStore(AgentCliHomeProvider home)
    {
        home.Ensure();
        _path = Path.GetFullPath(Path.Join(home.HomePath, ".claude-oauth-token"));
        if (!AgentCliHomeProvider.IsUnderRoot(home.HomePath, _path))
            throw new InvalidOperationException("The Claude subscription token file must stay under the agent CLI home.");
    }

    public string? Read()
    {
        try
        {
            if (!File.Exists(_path))
                return null;
            var token = File.ReadAllText(_path).Trim();
            return LooksLikeClaudeSubscriptionToken(token) ? token : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Write(string token)
    {
        if (!LooksLikeClaudeSubscriptionToken(token))
            throw new InvalidOperationException("Claude subscription login did not return a Claude Pro OAuth token.");
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(_path, token.Trim());
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    internal static bool LooksLikeClaudeSubscriptionToken(string token) =>
        token.StartsWith("sk-ant-oat", StringComparison.Ordinal);
}
