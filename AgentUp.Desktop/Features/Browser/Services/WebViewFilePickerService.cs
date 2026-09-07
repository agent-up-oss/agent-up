using Avalonia.Platform.Storage;
using AgentUp.Desktop.Features.Browser.DTOs;
using AgentUp.Desktop.Features.Browser.Providers;

namespace AgentUp.Desktop.Features.Browser.Services;

internal static class WebViewFilePickerService
{
    internal static string InstallScript => WebViewFilePickerProvider.InstallScript;

    internal static bool TryParseRequest(string? body, out WebViewFilePickerRequest? request) =>
        WebViewFilePickerProvider.TryParseRequest(body, out request);

    internal static async Task<string> BuildCompletionScriptAsync(
        string requestId,
        IReadOnlyList<IStorageFile> files,
        CancellationToken cancellationToken)
    {
        var selected = await WebViewFilePickerProvider.ReadFilesAsync(files, cancellationToken);
        return selected.Count == 0
            ? WebViewFilePickerProvider.CancelScript(requestId)
            : WebViewFilePickerProvider.CompleteScript(requestId, selected);
    }

    internal static string CancelScript(string requestId) => WebViewFilePickerProvider.CancelScript(requestId);
}
