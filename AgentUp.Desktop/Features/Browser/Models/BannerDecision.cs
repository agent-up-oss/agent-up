namespace AgentUp.Desktop.Features.Browser.Models;

// Decision produced by MainWindow.ResolveBannerDecision. Fully describes which banners
// to show and their content — no Avalonia control references, so it can be tested without
// a UI tree.
internal sealed record BannerDecision(
    bool ShowConnecting,
    string ConnectingText,
    bool ShowDownload,
    string DownloadText,
    bool DownloadFailed,
    int DownloadProgress);
