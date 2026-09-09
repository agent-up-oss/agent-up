using System.Text.Json;
using Avalonia.Platform.Storage;
using AgentUp.Desktop.Features.Browser.DTOs;

namespace AgentUp.Desktop.Features.Browser.Providers;

internal static class WebViewFilePickerProvider
{
    private const string MessageType = "agent-up:file-picker";
    private const long MaximumFileBytes = 32 * 1024 * 1024;
    private const long MaximumSelectionBytes = 128 * 1024 * 1024;
    private const int CopyBufferBytes = 81_920;
    private const string SelectionTooLargeMessage =
        "Upload selections are limited to 32 MB per file and 128 MB in total.";

    internal const string InstallScript =
        "(function(){" +
        "if(window.__agentUpFilePickerInstalled)return;window.__agentUpFilePickerInstalled=true;" +
        "window.__agentUpFilePickerRequests=new Map();" +
        "document.addEventListener('click',function(e){" +
        "var input=e.target&&e.target.closest?e.target.closest('input[type=file]'):null;" +
        "if(!e.isTrusted||!input||input.disabled||input.webkitdirectory)return;" +
        "e.preventDefault();e.stopImmediatePropagation();" +
        "var id=(crypto.randomUUID?crypto.randomUUID():Date.now()+'-'+Math.random());" +
        "window.__agentUpFilePickerRequests.set(id,input);" +
        "window.invokeCSharpAction(JSON.stringify({type:'agent-up:file-picker',requestId:id,multiple:input.multiple}));" +
        "},true);" +
        "})()";

    internal static bool TryParseRequest(string? body, out WebViewFilePickerRequest? request)
    {
        request = null;
        try
        {
            if (body is null) return false;
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type)
                || type.GetString() != MessageType
                || !root.TryGetProperty("requestId", out var requestId)
                || string.IsNullOrWhiteSpace(requestId.GetString()))
                return false;

            request = new WebViewFilePickerRequest(
                requestId.GetString()!,
                root.TryGetProperty("multiple", out var multiple) && multiple.ValueKind == JsonValueKind.True);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static async Task<IReadOnlyList<WebViewSelectedFile>> ReadFilesAsync(
        IReadOnlyList<IStorageFile> files,
        CancellationToken cancellationToken = default)
    {
        var selected = new List<WebViewSelectedFile>(files.Count);
        var remainingBytes = MaximumSelectionBytes;
        foreach (var file in files)
        {
            var content = await ReadBoundedContentAsync(file, Math.Min(MaximumFileBytes, remainingBytes), cancellationToken);
            remainingBytes -= content.Bytes;
            selected.Add(new WebViewSelectedFile(file.Name, GetMimeType(file.Name), content.Base64));
        }

        return selected;
    }

    // The limits are enforced while the stream is consumed rather than after it: a picker
    // selection is user-supplied and may point at an arbitrarily large file, and buffering
    // that whole file before rejecting it would exhaust Desktop's memory.
    private static async Task<(string Base64, long Bytes)> ReadBoundedContentAsync(
        IStorageFile file,
        long limitBytes,
        CancellationToken cancellationToken)
    {
        await using var input = await file.OpenReadAsync();
        using var output = new MemoryStream();
        var buffer = new byte[CopyBufferBytes];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            if (output.Length + read > limitBytes)
                throw new InvalidDataException(SelectionTooLargeMessage);

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var length = checked((int)output.Length);
        return (Convert.ToBase64String(output.GetBuffer(), 0, length), length);
    }

    internal static string CompleteScript(string requestId, IReadOnlyList<WebViewSelectedFile> files)
    {
        var id = JsonSerializer.Serialize(requestId);
        var payload = JsonSerializer.Serialize(files);
        return "(function(){var id=" + id + ";var input=window.__agentUpFilePickerRequests&&window.__agentUpFilePickerRequests.get(id);" +
               "if(!input)return;window.__agentUpFilePickerRequests.delete(id);" +
               "var transfer=new DataTransfer();" +
               "for(var f of " + payload + "){var raw=atob(f.Base64Content);var bytes=new Uint8Array(raw.length);" +
               "for(var i=0;i<raw.length;i++)bytes[i]=raw.charCodeAt(i);transfer.items.add(new File([bytes],f.Name,{type:f.MimeType}));}" +
               "input.files=transfer.files;input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));})()";
    }

    internal static string CancelScript(string requestId) =>
        "(function(){var id=" + JsonSerializer.Serialize(requestId) +
        ";if(window.__agentUpFilePickerRequests)window.__agentUpFilePickerRequests.delete(id);})()";

    private static string GetMimeType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".gif" => "image/gif",
        ".jpeg" or ".jpg" => "image/jpeg",
        ".json" => "application/json",
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".txt" => "text/plain",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
