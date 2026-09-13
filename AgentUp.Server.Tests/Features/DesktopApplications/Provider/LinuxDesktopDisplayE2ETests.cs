using System.ComponentModel;
using System.Diagnostics;
using AgentUp.Server.Features.DesktopApplications.Models;
using AgentUp.Server.Features.DesktopApplications.Providers;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Provider;

[TestFixture]
[Platform(Include = "Linux")]
public sealed class LinuxDesktopDisplayE2ETests
{
    private static readonly string[] DisplayClients = ["xclock", "xlogo", "xeyes"];

    [Test]
    public void StartAsync_rejectsADisplaySmallerThanTheMinimum()
    {
        var provider = new LinuxX11DesktopDisplayProvider(new PngFrameProvider());
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.StartAsync(100, 100, CancellationToken.None));
    }

    [Test]
    public async Task Captures_and_controls_a_real_x11_application()
    {
        var provider = new LinuxX11DesktopDisplayProvider(new PngFrameProvider());
        DesktopDisplayHandle display;
        try
        {
            display = await provider.StartAsync(640, 480, CancellationToken.None);
        }
        catch (DllNotFoundException)
        {
            Assert.Ignore("libX11 is not available. On NixOS run this suite through nix-shell shell.nix.");
            return;
        }

        using var application = StartDisplayClient(display.DisplayName);

        try
        {
            if (application is not null)
                await Task.Delay(400);

            var screenshot = await provider.CapturePngAsync(display, CancellationToken.None);

            Assert.That(screenshot.Take(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
            Assert.That(screenshot.Length, Is.GreaterThan(1_000));

            try
            {
                await provider.SendPointerAsync(display, 320, 240, 0, true, CancellationToken.None);
                await provider.SendPointerAsync(display, 320, 240, 0, false, CancellationToken.None);
                await provider.SendKeyAsync(display, "Escape", true, CancellationToken.None);
                await provider.SendKeyAsync(display, "Escape", false, CancellationToken.None);
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await provider.SendPointerAsync(display, -1, 0, 0, true, CancellationToken.None));
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await provider.SendKeyAsync(display, string.Empty, true, CancellationToken.None));
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await provider.SendKeyAsync(display, "DefinitelyNotAKey", true, CancellationToken.None));
            }
            catch (DllNotFoundException)
            {
                Assert.Ignore("libXtst is not available. On NixOS run this suite through nix-shell shell.nix.");
            }
        }
        finally
        {
            if (application is { HasExited: false })
                application.Kill(entireProcessTree: true);
            await provider.StopAsync(display, CancellationToken.None);
        }
    }

    private static Process? StartDisplayClient(string displayName)
    {
        var fileName = FindDisplayClient();
        if (fileName is null)
            return null;

        var application = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };
        application.StartInfo.Environment["DISPLAY"] = displayName;

        try
        {
            application.Start();
            return application;
        }
        catch (Win32Exception)
        {
            application.Dispose();
            return null;
        }
    }

    private static string? FindDisplayClient()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(directory => DisplayClients.Select(fileName => Path.Join(directory, fileName)))
            .FirstOrDefault(File.Exists);
    }
}
