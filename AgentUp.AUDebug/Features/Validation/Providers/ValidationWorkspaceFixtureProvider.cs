using System.Text;
using System.Text.Json;
using AgentUp.AUDebug.Features.Validation.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Validation.Providers;

public sealed class ValidationWorkspaceFixtureProvider(
    IDebugPathValidator paths,
    HttpMessageHandler? handler = null) : IValidationWorkspaceFixture
{
    public const string WorkspaceDisplayName = "Agent Up";
    public const string ScriptedAgentId = "test-agent-scripted";
    public const string ScriptedAgentCommandKey = "Agents__test-agent-scripted__Command";

    private static readonly HashSet<string> ReservedConfigKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "display",
        "prompts",
        "commits",
        "verification",
        "coverage"
    };

    public string CreateDataDirectory()
    {
        var root = Path.Join(Path.GetTempPath(), "agent-up-desktop-validate", Guid.NewGuid().ToString("N"));
        var data = Path.Join(root, "data");
        Directory.CreateDirectory(data);
        return data;
    }

    public IReadOnlyDictionary<string, string> ServerEnvironment(string dataDirectory)
    {
        var bin = Path.Join(Directory.GetParent(dataDirectory)?.FullName ?? dataDirectory, "bin");
        Directory.CreateDirectory(bin);
        var shim = WriteScriptedAgentShim(bin, FindTestAgentsExecutable());
        var path = bin + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? "");
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = path,
            [ScriptedAgentCommandKey] = shim
        };
    }

    public async Task PrepareAsync(string serverUrl, CancellationToken cancellationToken)
    {
        var baseAddress = new Uri(serverUrl.TrimEnd('/') + "/");
        using var http = handler is null
            ? new HttpClient { Timeout = TimeSpan.FromSeconds(30), BaseAddress = baseAddress }
            : new HttpClient(handler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(30),
                BaseAddress = baseAddress
            };
        using var content = new StringContent(RegistrationJson(), Encoding.UTF8, "application/json");
        using var response = await http.PostAsync("api/workspaces", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Could not register the validation workspace: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }
    }

    private string RegistrationJson()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("displayName", WorkspaceDisplayName);
        writer.WriteString("repositoryPath", paths.RepositoryRoot);
        writer.WriteString("worktreePath", paths.RepositoryRoot);
        writer.WriteString("branch", "main");
        writer.WriteString("commit", "0000000000000000000000000000000000000000");
        WriteConfigArrays(writer);
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteConfigArrays(Utf8JsonWriter writer)
    {
        var path = paths.JoinUnderRoot("agent-up.json");
        if (!File.Exists(path))
            return;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not read agent-up.json: {exception.Message}", exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in document.RootElement.EnumerateObject()
                         .Where(item => !ReservedConfigKeys.Contains(item.Name) && item.Value.ValueKind == JsonValueKind.Array))
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
        }
    }

    private string FindTestAgentsExecutable()
    {
        var found = new[] { "Debug", "Release" }
            .Select(configuration => paths.JoinUnderRoot(
                "AgentUp.TestAgents", "bin", configuration, "net10.0", "AgentUp.TestAgents"))
            .Select(path => OperatingSystem.IsWindows() ? path + ".exe" : path)
            .FirstOrDefault(File.Exists);
        if (found is null)
        {
            throw new InvalidOperationException(
                "Could not find AgentUp.TestAgents. Build AgentUp.TestAgents before au-debug validate.");
        }

        return found;
    }

    private static string WriteScriptedAgentShim(string bin, string executable)
    {
        var shim = Path.Join(bin, OperatingSystem.IsWindows() ? $"{ScriptedAgentId}.cmd" : ScriptedAgentId);
        try
        {
            File.WriteAllText(shim, OperatingSystem.IsWindows()
                ? $"@echo off{Environment.NewLine}set AGENTUP_TEST_AGENT={ScriptedAgentId}{Environment.NewLine}\"{executable}\" %*{Environment.NewLine}"
                : $"#!/bin/sh{Environment.NewLine}set -e{Environment.NewLine}export AGENTUP_TEST_AGENT='{ScriptedAgentId}'{Environment.NewLine}exec '{executable}' \"$@\"{Environment.NewLine}");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    shim,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not write {ScriptedAgentId} shim: {exception.Message}", exception);
        }

        return shim;
    }
}
