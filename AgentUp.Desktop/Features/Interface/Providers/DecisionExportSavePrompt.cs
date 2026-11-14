using System.Text;
using AgentUp.Desktop.Features.Interface.Interfaces;
using Avalonia.Platform.Storage;

namespace AgentUp.Desktop.Features.Interface.Providers;

/// <summary>
/// Saves an export where the reader puts it.
///
/// The backlog is training material rather than something the app consumes again, so it goes to a
/// place of the reader's choosing instead of a folder only this app knows about.
/// </summary>
public sealed class DecisionExportSavePrompt(IStorageProvider storage) : IDecisionExportSink
{
    public async Task<bool> SaveAsync(
        string suggestedFileName,
        string jsonl,
        CancellationToken cancellationToken = default)
    {
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Product Decisions",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "jsonl",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON Lines") { Patterns = ["*.jsonl"] }
            ]
        });
        if (file is null)
            return false;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(jsonl), cancellationToken);
        return true;
    }
}
