using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Tests.Support;

namespace LocalInstaller.Core.Tests.Features.Installation.Unit;

[TestFixture]
public class PayloadSelectionTests
{
    private const string ProductName = "Agent-Up";

    [Test]
    public void BundledPayload_describesOfflineInstallPayload()
    {
        var payload = AgentUpTestManifests.BundledPayload(new Version(1, 2, 3));

        Assert.That(payload.Source, Is.EqualTo(PayloadSourceKind.Bundled));
        Assert.That(payload.DownloadUrl, Is.Null);
        Assert.That(payload.Description, Does.Contain("Bundled"));
    }

    [Test]
    public void OnlinePayload_keepsDownloadUrlForUpdateFlow()
    {
        var payload = PayloadSelection.Online(ProductName, new Version(1, 2, 4), "https://example.invalid/agent-up.zip");

        Assert.That(payload.Source, Is.EqualTo(PayloadSourceKind.Online));
        Assert.That(payload.DownloadUrl, Is.EqualTo("https://example.invalid/agent-up.zip"));
    }
}
