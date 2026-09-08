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
        return WithDocumentLock(document =>
            document.Servers.TryGetValue(normalized, out var token) ? token : null);
    }

    public void SetToken(string serverUrl, string token)
    {
        var normalized = ServerUrlNormalizer.Normalize(serverUrl);
        WithDocumentLock(document =>
        {
            document.Servers[normalized] = token;
            WriteDocument(document);
        });
    }

    public void ClearToken(string serverUrl)
    {
        var normalized = ServerUrlNormalizer.Normalize(serverUrl);
        WithDocumentLock(document =>
        {
            if (!document.Servers.Remove(normalized))
                return;

            WriteDocument(document);
        });
    }

    private T WithDocumentLock<T>(Func<CredentialsDocument, T> mutate)
    {
        var lockPath = $"{_filePath}.lock";
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        RestrictDirectoryAccess(directory);

        using var lockStream = new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        return mutate(ReadDocument());
    }

    private void WithDocumentLock(Action<CredentialsDocument> mutate)
        => WithDocumentLock(document =>
        {
            mutate(document);
            return 0;
        });

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
        RestrictDirectoryAccess(directory);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var tempPath = Path.Join(directory, $"{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            WriteOwnerOnlyText(tempPath, json);
            File.Move(tempPath, _filePath, true);
            RestrictFileAccess(_filePath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void WriteOwnerOnlyText(string path, string contents)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, contents);
            return;
        }

        using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
            });
        using var writer = new StreamWriter(stream);
        writer.Write(contents);
    }

    private static void RestrictDirectoryAccess(string directory)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void RestrictFileAccess(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
