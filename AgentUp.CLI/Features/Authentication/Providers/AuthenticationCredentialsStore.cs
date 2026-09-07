using System.Text.Json;
using AgentUp.CLI.Features.Authentication.Models;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Features.Authentication.Providers;

public sealed class AuthenticationCredentialsStore(string? baseDirectory = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath = Path.Join(
        baseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "agentup",
        "cli-auth.json");

    public string? GetToken(string serverUrl)
    {
        var normalized = ServerUrlNormalizer.Normalize(serverUrl);
        var document = ReadDocument();
        return document.Servers.TryGetValue(normalized, out var token) ? token : null;
    }

    public void SetToken(string serverUrl, string token)
    {
        var normalized = ServerUrlNormalizer.Normalize(serverUrl);
        var document = ReadDocument();
        document.Servers[normalized] = token;
        WriteDocument(document);
    }

    public void ClearToken(string serverUrl)
    {
        var normalized = ServerUrlNormalizer.Normalize(serverUrl);
        var document = ReadDocument();
        if (!document.Servers.Remove(normalized))
            return;

        WriteDocument(document);
    }

    private CredentialsDocument ReadDocument()
    {
        if (!File.Exists(_filePath))
            return new CredentialsDocument();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<CredentialsDocument>(json) ?? new CredentialsDocument();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new CredentialsDocument();
        }
    }

    private void WriteDocument(CredentialsDocument document)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var tempPath = Path.Join(directory, $"{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
