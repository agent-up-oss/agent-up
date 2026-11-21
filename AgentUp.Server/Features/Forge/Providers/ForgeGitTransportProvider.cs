using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AgentUp.Server.Features.Forge.Interfaces;

namespace AgentUp.Server.Features.Forge.Providers;

/// <summary>
/// Runs <c>git push</c> and <c>git fetch</c> against the remote URL with the credential sent as an
/// HTTP header through Git's environment configuration. The token never appears on a command line,
/// in the clone's config, or in the remote URL, so nothing on disk or in a process listing keeps it.
/// SSH remotes carry no header and fall back to the host's own Git credentials.
/// </summary>
public sealed class ForgeGitTransportProvider : IForgeGitTransport
{
    public Task PushAsync(string repositoryPath, string remoteUrl, string branch, string token, CancellationToken cancellationToken)
        => RunAsync(repositoryPath, ["push", "--no-verify", remoteUrl, $"HEAD:refs/heads/{branch}"], remoteUrl, token, cancellationToken);

    public Task FetchAsync(string repositoryPath, string remoteUrl, IReadOnlyList<string> branches, string token, CancellationToken cancellationToken)
        => RunAsync(
            repositoryPath,
            ["fetch", "--no-tags", remoteUrl, .. branches.Select(branch => $"+refs/heads/{branch}:refs/remotes/origin/{branch}")],
            remoteUrl,
            token,
            cancellationToken);

    /// <summary>The environment that hands Git the credential, or none for a remote that is not HTTP.</summary>
    internal static IReadOnlyDictionary<string, string> CredentialEnvironment(string remoteUrl, string token)
    {
        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return new Dictionary<string, string>();
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{token}"));
        return new Dictionary<string, string>
        {
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "http.extraHeader",
            ["GIT_CONFIG_VALUE_0"] = $"Authorization: Basic {basic}"
        };
    }

    private static async Task RunAsync(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        string remoteUrl,
        string token,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(Path.GetFullPath(repositoryPath));
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var (key, value) in CredentialEnvironment(remoteUrl, token))
            start.Environment[key] = value;

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("Git is not available on this Server host.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("Git is not available on this Server host.", exception);
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await output;
            var stderr = await error;
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"git {arguments[0]} failed: {stderr.Replace(token, "***", StringComparison.Ordinal).Trim()}");
        }
    }
}
