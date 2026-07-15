using AgentUp.Installers.Features.Payloads;

namespace AgentUp.Installers.Tests.Features.Payloads;

[TestFixture]
public class PayloadSelectionTests
{
    [Test]
    public void BundledPayload_describesOfflineInstallPayload()
    {
        var payload = PayloadSelection.Bundled(new Version(1, 2, 3));

        Assert.That(payload.Source, Is.EqualTo(PayloadSourceKind.Bundled));
        Assert.That(payload.DownloadUrl, Is.Null);
        Assert.That(payload.Description, Does.Contain("Bundled"));
    }

    [Test]
    public void OnlinePayload_keepsDownloadUrlForUpdateFlow()
    {
        var payload = PayloadSelection.Online(new Version(1, 2, 4), "https://example.invalid/agent-up.zip");

        Assert.That(payload.Source, Is.EqualTo(PayloadSourceKind.Online));
        Assert.That(payload.DownloadUrl, Is.EqualTo("https://example.invalid/agent-up.zip"));
    }
}
