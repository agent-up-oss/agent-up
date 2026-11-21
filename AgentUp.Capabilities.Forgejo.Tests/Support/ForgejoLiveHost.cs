using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;
using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Forgejo.Tests.Support;

/// <summary>
/// One Docker-hosted Forgejo instance shared by the live E2E fixtures. Missing Docker is a
/// failure, not a skip: the suite is the proof the first-party module talks to a real host.
/// </summary>
public sealed class ForgejoLiveHost : IAsyncDisposable
{
    public const string Image =
        "codeberg.org/forgejo/forgejo:11.0.6@sha256:71c020a9fb01593d5c9fbf2700b3b11dccc5d9e01f9a2b7dbb1d1f7ebb9cb58e";

    public const string Owner = "agentup";

    private const string Password = "AgentUpE2E.pass1";
    private const string Email = "agentup@example.invalid";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static ForgejoLiveHost? SharedInstance;

    private readonly string _container;
    private readonly HttpClient _client;
    private readonly List<string> _workDirectories = [];

    private ForgejoLiveHost(string container, int port, string token)
    {
        _container = container;
        Port = port;
        Token = token;
        Origin = $"http://127.0.0.1:{port}";
        _client = new HttpClient
        {
            BaseAddress = new Uri(Origin + "/api/v1/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("agent-up-forgejo-e2e");
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public int Port { get; }

    public string Origin { get; }

    public string Token { get; }

    public ForgeHostConfiguration Host => ForgejoHostConfigurationProvider.From(Origin, null, null, null);

    public static async Task<ForgejoLiveHost> SharedAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (SharedInstance is { } existing)
                return existing;

            var started = await StartAsync(cancellationToken);
            SharedInstance = started;
            return started;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task StopSharedAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (SharedInstance is null)
                return;
            await SharedInstance.DisposeAsync();
            SharedInstance = null;
        }
        finally
        {
            Gate.Release();
        }
    }

    public ForgejoForgeCapability Capability() => new(Host);

    public ForgeRequest Request(ForgejoLiveRepository repository, string branch)
        => new(Token, repository.Owner, repository.Name, branch);

    public async Task<ForgejoLiveRepository> CreateRepositoryAsync(
        string name,
        CancellationToken cancellationToken)
    {
        using var created = await _client.PostAsJsonAsync(
            "user/repos",
            new { name, @private = true, auto_init = false, default_branch = "main" },
            cancellationToken);
        created.EnsureSuccessStatusCode();

        var work = Directory.CreateDirectory(
            Path.Join(Path.GetTempPath(), "agent-up-forgejo-e2e-" + Guid.NewGuid().ToString("N"))).FullName;
        _workDirectories.Add(work);
        await WriteCommitAsync(work, "README.md", "widgets\n", "main");
        var topic = Path.Join(work, "topic.txt");
        await File.WriteAllTextAsync(topic, "topic\n", cancellationToken);
        await GitAsync(work, cancellationToken, "checkout", "-b", "topic");
        await GitAsync(work, cancellationToken, "add", "topic.txt");
        await GitAsync(work, cancellationToken, "commit", "-m", "topic");
        var clone = $"http://oauth2:{Token}@127.0.0.1:{Port}/{Owner}/{name}.git";
        await GitAsync(work, cancellationToken, "remote", "add", "origin", clone);
        await GitAsync(work, cancellationToken, "push", "-u", "origin", "main");
        await GitAsync(work, cancellationToken, "push", "-u", "origin", "topic");
        await WaitForBranchAsync(name, "main", cancellationToken);
        await WaitForBranchAsync(name, "topic", cancellationToken);
        var mainSha = await GitTextAsync(work, cancellationToken, "rev-parse", "main");
        var topicSha = await GitTextAsync(work, cancellationToken, "rev-parse", "topic");
        return new ForgejoLiveRepository(
            Owner,
            name,
            Origin,
            $"http://127.0.0.1:{Port}/{Owner}/{name}.git",
            mainSha,
            topicSha);
    }

    public async Task<int> CreateIssueAsync(
        string repository,
        string title,
        string body,
        string? assignee,
        CancellationToken cancellationToken)
    {
        using var response = await _client.PostAsJsonAsync(
            $"repos/{Owner}/{repository}/issues",
            assignee is null
                ? new { title, body }
                : (object)new { title, body, assignees = new[] { assignee } },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var issue = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        return issue.GetProperty("number").GetInt32();
    }

    public async Task CreateCommitStatusAsync(
        string repository,
        string sha,
        string context,
        string state,
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await _client.PostAsJsonAsync(
            $"repos/{Owner}/{repository}/statuses/{sha}",
            new { context, state, target_url = url },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A second account with write access to <paramref name="repository"/>, as a team member is:
    /// its own login and its own token, so what it does is attributed to it.
    /// </summary>
    public async Task<(string Login, string Token)> CreateCollaboratorAsync(string repository, string login, CancellationToken cancellationToken)
    {
        using (var user = await _client.PostAsJsonAsync(
            "admin/users",
            new { username = login, email = $"{login}@example.invalid", password = Password, must_change_password = false },
            cancellationToken))
        {
            if (user.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
                user.EnsureSuccessStatusCode();
        }

        using (var collaborator = await _client.PutAsJsonAsync(
            $"repos/{Owner}/{repository}/collaborators/{login}",
            new { permission = "write" },
            cancellationToken))
            collaborator.EnsureSuccessStatusCode();

        using var basic = new HttpClient { BaseAddress = new Uri($"{Origin}/api/v1/") };
        basic.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{login}:{Password}")));
        using var token = await basic.PostAsJsonAsync(
            $"users/{login}/tokens",
            new { name = $"e2e-{repository}", scopes = new[] { "all" } },
            cancellationToken);
        token.EnsureSuccessStatusCode();
        var created = await token.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        return (login, created.GetProperty("sha1").GetString()!);
    }

    /// <summary>A JSON document the owner reads from the API, for asserting what the host shows.</summary>
    public async Task<JsonElement> ReadAsync(string path, CancellationToken cancellationToken)
        => await _client.GetFromJsonAsync<JsonElement>(path, Json, cancellationToken);

    public async Task WaitForBranchAsync(string repository, string branch, CancellationToken cancellationToken)
        => await ReadBranchShaAsync(repository, branch, cancellationToken);

    public async Task<string> ReadBranchShaAsync(string repository, string branch, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var response = await _client.GetAsync($"repos/{Owner}/{repository}/branches/{branch}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var document = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
                return document.GetProperty("commit").GetProperty("id").GetString() ?? "";
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new TimeoutException($"Forgejo never advertised {Owner}/{repository}:{branch}.");
    }

    private static async Task<ForgejoLiveHost> StartAsync(CancellationToken cancellationToken)
    {
        var container = "agent-up-forgejo-e2e-" + Guid.NewGuid().ToString("N")[..12];
        var port = FreePort();
        try
        {
            await DockerAsync(
                cancellationToken,
                "run",
                "-d",
                "--name",
                container,
                "-p",
                $"127.0.0.1:{port}:3000",
                "-e",
                "FORGEJO__database__DB_TYPE=sqlite3",
                "-e",
                "FORGEJO__server__HTTP_PORT=3000",
                "-e",
                $"FORGEJO__server__ROOT_URL=http://127.0.0.1:{port}/",
                "-e",
                "FORGEJO__server__DISABLE_SSH=true",
                "-e",
                "FORGEJO__server__OFFLINE_MODE=true",
                "-e",
                "FORGEJO__service__DISABLE_REGISTRATION=true",
                "-e",
                "FORGEJO__security__INSTALL_LOCK=true",
                Image);
            await WaitUntilReadyAsync(port, cancellationToken);
            await DockerAsync(
                cancellationToken,
                "exec",
                "-u",
                "git",
                container,
                "forgejo",
                "admin",
                "user",
                "create",
                "--admin",
                "--username",
                Owner,
                "--password",
                Password,
                "--email",
                Email,
                "--must-change-password=false");
            var token = await DockerTextAsync(
                cancellationToken,
                "exec",
                "-u",
                "git",
                container,
                "forgejo",
                "admin",
                "user",
                "generate-access-token",
                "--username",
                Owner,
                "--token-name",
                "e2e",
                "--scopes",
                "all",
                "--raw");
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Forgejo did not print an access token.");
            return new ForgejoLiveHost(container, port, token.Trim());
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                              or HttpRequestException
                                              or TimeoutException
                                              or TaskCanceledException
                                              or Win32Exception
                                              or IOException)
        {
            await TryRemoveAsync(container);
            throw;
        }
    }

    private static async Task WaitUntilReadyAsync(int port, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var url = $"http://127.0.0.1:{port}/api/v1/version";
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return;
                last = new HttpRequestException($"Forgejo version probe returned {(int)response.StatusCode}.");
            }
            catch (HttpRequestException exception)
            {
                last = exception;
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                last = exception;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"Forgejo on 127.0.0.1:{port} never became ready.", last);
    }

    private static async Task WriteCommitAsync(string work, string fileName, string contents, string branch)
    {
        Directory.CreateDirectory(work);
        await GitAsync(work, CancellationToken.None, "init", "--initial-branch", branch);
        await GitAsync(work, CancellationToken.None, "config", "user.name", "Agent Up");
        await GitAsync(work, CancellationToken.None, "config", "user.email", "agent-up@example.invalid");
        await GitAsync(work, CancellationToken.None, "config", "commit.gpgsign", "false");
        await File.WriteAllTextAsync(Path.Join(work, fileName), contents);
        await GitAsync(work, CancellationToken.None, "add", fileName);
        await GitAsync(work, CancellationToken.None, "commit", "-m", branch);
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static Task DockerAsync(CancellationToken cancellationToken, params string[] arguments)
        => RunAsync("docker", arguments, cancellationToken);

    private static Task<string> DockerTextAsync(CancellationToken cancellationToken, params string[] arguments)
        => RunTextAsync("docker", arguments, cancellationToken);

    private static Task GitAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
        => RunAsync("git", arguments, cancellationToken, workingDirectory);

    private static Task<string> GitTextAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
        => RunTextAsync("git", arguments, cancellationToken, workingDirectory);

    private static async Task RunAsync(
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        var (stdout, stderr, exitCode) = await ExecuteAsync(fileName, arguments, cancellationToken, workingDirectory);
        if (exitCode != 0)
            throw new InvalidOperationException($"{fileName} {string.Join(" ", arguments)} failed: {stderr.Trim()}\n{stdout.Trim()}");
    }

    private static async Task<string> RunTextAsync(
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        var (stdout, stderr, exitCode) = await ExecuteAsync(fileName, arguments, cancellationToken, workingDirectory);
        if (exitCode != 0)
            throw new InvalidOperationException($"{fileName} {string.Join(" ", arguments)} failed: {stderr.Trim()}\n{stdout.Trim()}");
        return stdout.Trim();
    }

    private static async Task<(string Stdout, string Stderr, int ExitCode)> ExecuteAsync(
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken,
        string? workingDirectory)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            psi.WorkingDirectory = workingDirectory;
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        Process process;
        try
        {
            process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                $"Forgejo E2E requires {fileName} on PATH. {exception.Message}",
                exception);
        }

        using (process)
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return (await stdoutTask, await stderrTask, process.ExitCode);
        }
    }

    private static async Task<bool> TryRemoveAsync(string container)
    {
        try
        {
            await DockerAsync(CancellationToken.None, "rm", "-f", container);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        foreach (var directory in _workDirectories.Where(Directory.Exists))
            Directory.Delete(directory, recursive: true);

        await TryRemoveAsync(_container);
    }
}

public sealed record ForgejoLiveRepository(
    string Owner,
    string Name,
    string Origin,
    string CloneUrl,
    string MainSha,
    string TopicSha);
