using System.Text;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using Avalonia.Platform.Storage;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

public sealed class ReviewReportSavePrompt(IStorageProvider storage) : IReviewReportSavePrompt
{
    public async Task<bool> SaveAsync(string suggestedFileName, string html, CancellationToken cancellationToken = default)
    {
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Report",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "html",
            FileTypeChoices =
            [
                new FilePickerFileType("HTML") { Patterns = ["*.html"] }
            ]
        });
        if (file is null)
            return false;

        await using var stream = await file.OpenWriteAsync();
        var bytes = Encoding.UTF8.GetBytes(html);
        await stream.WriteAsync(bytes, cancellationToken);
        return true;
    }
}
