using AgentUp.Browser.Streaming.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Browser.Streaming.Tests.Features.RemoteDisplay.Unit;

[TestFixture]
public sealed class BrowserRemoteDisplayServiceTests
{
    [Test]
    public async Task Connected_viewer_receives_control_text_and_binary_frames()
    {
        var service = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        using var connection = new RecordingSubscriberConnection();
        using var cancellation = new CancellationTokenSource();
        var subscription = service.ConnectAsync("workspace", connection, null, cancellation.Token);
        await WaitUntilAsync(() => service.HasSubscribers("workspace"));

        await service.BroadcastTextAsync("workspace", "control", CancellationToken.None);
        await service.BroadcastFrameAsync("workspace", [1, 2, 3], CancellationToken.None);
        await WaitUntilAsync(() => connection.Messages.Any(message => message.Kind == "binary"));

        Assert.Multiple(() =>
        {
            Assert.That(connection.Messages.Any(message => message.Kind == "text"), Is.True);
            Assert.That(connection.Messages.Single(message => message.Kind == "binary").Payload,
                Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(service.TryGetLatestFrame("workspace", out var frame), Is.True);
            Assert.That(frame, Is.EqualTo(new byte[] { 1, 2, 3 }));
        });

        await cancellation.CancelAsync();
        await subscription;
        Assert.That(service.HasSubscribers("workspace"), Is.False);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }
}
