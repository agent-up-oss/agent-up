using System.ComponentModel;
using System.Diagnostics;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Headless Chromium screenshot, the same flags the product-screen capture uses, aimed at
/// the summary page rather than a long scrolling document.
/// </summary>
public sealed class ChromiumReportPageCapture : IReportPageCapture
{
    private static readonly string[] Names = ["chromium", "chromium-browser", "google-chrome", "google-chrome-stable"];
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(25);

    private readonly TimeSpan _timeout;
    private readonly string? _path;

    public ChromiumReportPageCapture()
        : this(DefaultTimeout, null)
    {
    }

    internal ChromiumReportPageCapture(TimeSpan timeout, string? path)
    {
        _timeout = timeout;
        _path = path;
    }

    public async Task CaptureAsync(
        string htmlPath,
        string pngPath,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var chromium = Find(_path ?? Environment.GetEnvironmentVariable("PATH"));
        if (chromium is null)
            throw new ReportCaptureException("Chromium is not installed, so report.png was not written. report.json and report.html were.");

        var profile = Path.Join(Path.GetTempPath(), "au-check-chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        var arguments = Arguments(htmlPath, pngPath, profile, width, height);

        try
        {
            var exitCode = await RunAsync(chromium, arguments, _timeout, cancellationToken);
            if (exitCode != 0 || !File.Exists(pngPath))
                throw new ReportCaptureException($"Chromium did not write '{pngPath}' (exit {exitCode}).");
        }
        catch (Win32Exception exception)
        {
            throw new ReportCaptureException($"Could not start Chromium: {exception.Message}");
        }
        finally
        {
            _ = DeleteProfile(profile);
        }
    }

    private static bool DeleteProfile(string profile)
    {
        try
        {
            Directory.Delete(profile, recursive: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<int> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        if (!process.Start())
            throw new ReportCaptureException("Chromium did not start.");

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _ = Stop(process);
            throw new ReportCaptureException("Chromium did not finish the screenshot.");
        }

        return process.ExitCode;
    }

    private static bool Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static string? Find(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in Names)
            {
                var candidate = Path.Join(directory, name);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    internal static IReadOnlyList<string> Arguments(string htmlPath, string pngPath, string profile, int width, int height)
        =>
        [
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            "--hide-scrollbars",
            "--force-device-scale-factor=1",
            "--disable-lcd-text",
            "--font-render-hinting=none",
            $"--window-size={width},{height}",
            "--virtual-time-budget=8000",
            "--run-all-compositor-stages-before-draw",
            $"--screenshot={pngPath}",
            "--timeout=25000",
            $"--user-data-dir={profile}",
            new Uri(htmlPath).AbsoluteUri
        ];
}
