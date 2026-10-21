using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Models;

namespace AgentUp.Server.Features.Processes.Repositories;

public sealed class FileOutputRepository : IOutputRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _outputRoot;
    private readonly int _capacity;
    private readonly ConcurrentDictionary<(string, string), SemaphoreSlim> _gates = new();
    private readonly ConcurrentDictionary<(string, string), ProcessOutputBuffer> _buffers = new();

    public FileOutputRepository(string baseDir, int capacity = ProcessOutputBuffer.DefaultCapacity)
    {
        _outputRoot = Path.GetFullPath(Path.Join(baseDir, "output"));
        _capacity = capacity;
    }

    public async Task AppendAsync(string workspaceId, string appName, ProcessOutputLine line, CancellationToken ct = default)
    {
        var key = (workspaceId, appName);
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var buffer = await LoadAsync(key, ct);
            buffer.Append(line);
            var path = GetPath(workspaceId, appName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.AppendAllTextAsync(path, Encode(line) + "\n", ct);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<ProcessOutputLine>> GetAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        var key = (workspaceId, appName);
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var buffer = await LoadAsync(key, ct);
            return buffer.Snapshot();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        var key = (workspaceId, appName);
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_buffers.TryGetValue(key, out var buffer))
                buffer.Clear();

            var path = GetPath(workspaceId, appName);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ProcessOutputBuffer> LoadAsync((string WorkspaceId, string AppName) key, CancellationToken ct)
    {
        if (_buffers.TryGetValue(key, out var existing))
            return existing;

        var buffer = new ProcessOutputBuffer(_capacity);
        var path = GetPath(key.WorkspaceId, key.AppName);
        if (File.Exists(path))
        {
            foreach (var storageLine in await File.ReadAllLinesAsync(path, ct))
                buffer.Append(Decode(storageLine));
        }

        return _buffers.GetOrAdd(key, buffer);
    }

    internal static string Encode(ProcessOutputLine line)
        => JsonSerializer.Serialize(
            new Dictionary<string, string> { ["text"] = line.Text, ["stream"] = line.StreamName },
            Json);

    internal static ProcessOutputLine Decode(string storageLine)
    {
        if (string.IsNullOrWhiteSpace(storageLine))
            return new ProcessOutputLine(string.Empty, ProcessOutputStream.Stdout);

        if (!storageLine.StartsWith('{'))
            return new ProcessOutputLine(storageLine, ProcessOutputStream.Stdout);

        try
        {
            using var document = JsonDocument.Parse(storageLine);
            var text = document.RootElement.TryGetProperty("text", out var textNode)
                ? textNode.GetString() ?? string.Empty
                : string.Empty;
            var streamName = document.RootElement.TryGetProperty("stream", out var streamNode)
                ? streamNode.GetString()
                : null;
            var stream = string.Equals(streamName, "stderr", StringComparison.OrdinalIgnoreCase)
                ? ProcessOutputStream.Stderr
                : ProcessOutputStream.Stdout;
            return new ProcessOutputLine(text, stream);
        }
        catch (JsonException)
        {
            return new ProcessOutputLine(storageLine, ProcessOutputStream.Stdout);
        }
    }

    private string GetPath(string workspaceId, string appName)
    {
        var fileName = GetLogFileName(workspaceId, appName);
        var path = Path.GetFullPath(Path.Join(_outputRoot, fileName));
        var root = _outputRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _outputRoot
            : _outputRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!path.StartsWith(root, comparison))
            throw new InvalidOperationException("Output log path escaped the output root.");

        return path;
    }

    private static string GetLogFileName(string workspaceId, string appName)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("Application name is required.", nameof(appName));

        var bytes = Encoding.UTF8.GetBytes(workspaceId + "\0" + appName);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".log";
    }
}
