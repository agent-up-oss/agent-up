using System.Diagnostics;
using AgentUp.Server.Features.DesktopApplications.Providers;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Provider;

[TestFixture]
[Platform(Include = "Linux")]
public sealed class LinuxDesktopDisplayE2ETests
{
    [Test]
    public async Task Captures_and_controls_a_real_x11_application()
    {
        var provider = new LinuxX11DesktopDisplayProvider(new PngFrameProvider());
        var display = await provider.StartAsync(640, 480, CancellationToken.None);
        using var application = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "xclock",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };
        application.StartInfo.Environment["DISPLAY"] = display.DisplayName;
        var started = false;

        try
        {
            application.Start();
            started = true;
            await Task.Delay(400);
            var screenshot = await provider.CapturePngAsync(display, CancellationToken.None);

            Assert.That(screenshot.Take(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
            Assert.That(screenshot.Length, Is.GreaterThan(1_000));

            await provider.SendPointerAsync(display, 320, 240, 0, true, CancellationToken.None);
            await provider.SendPointerAsync(display, 320, 240, 0, false, CancellationToken.None);
            await provider.SendKeyAsync(display, "Escape", true, CancellationToken.None);
            await provider.SendKeyAsync(display, "Escape", false, CancellationToken.None);
        }
        finally
        {
            if (started && !application.HasExited) application.Kill(entireProcessTree: true);
            await provider.StopAsync(display, CancellationToken.None);
        }
    }
}
