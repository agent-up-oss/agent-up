using Avalonia.Platform.Storage;
using AgentUp.Desktop.Features.Browser.DTOs;
using AgentUp.Desktop.Features.Browser.Services;

namespace AgentUp.Desktop.Features.Browser.Controllers;

public sealed class WebViewFilePickerController
{
    internal string InstallScript => WebViewFilePickerService.InstallScript;

    internal bool TryParseRequest(string? body, out WebViewFilePickerRequest? request) =>
        WebViewFilePickerService.TryParseRequest(body, out request);

    internal async Task<string> BuildCompletionScriptAsync(
        string requestId,
        IReadOnlyList<IStorageFile> files,
        CancellationToken cancellationToken = default)
    {
        return await WebViewFilePickerService.BuildCompletionScriptAsync(
            requestId,
            files,
            cancellationToken);
    }

    internal string CancelScript(string requestId) => WebViewFilePickerService.CancelScript(requestId);
}
