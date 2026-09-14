using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Authentication.Providers;

/// <summary>
/// Where a test agent keeps its credentials, under <c>HOME</c> the way the real CLIs do. The
/// Server redirects <c>HOME</c> into its data directory, so this is also what proves the Server's
/// redirection works: an agent that signed in finds its token again on the next launch.
/// </summary>
public sealed class TestAgentCredentialStore : ITestAgentCredentialStore
{
    private readonly string _path;

    /// <param name="schema">Which agent this is; each keeps its own credential.</param>
    /// <param name="home">
    /// Where to keep it. Passed in rather than read from the environment on every access, because
    /// the runtime caches the resolved home directory and a test that changed it afterwards would
    /// be reading a stale path.
    /// </param>
    public TestAgentCredentialStore(TestAgentSchema schema, string? home = null)
    {
        var root = home ?? Environment.GetEnvironmentVariable("HOME")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var name = schema.ToString().ToLowerInvariant();
        _path = System.IO.Path.Join(root, $".agent-up-{name}-credentials");
    }

    private string Path => _path;

    public string? Read()
    {
        try
        {
            var path = Path;
            if (!File.Exists(path))
                return null;
            var token = File.ReadAllText(path).Trim();
            return token.Length == 0 ? null : token;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Write(string token)
    {
        var path = Path;
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, token);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
