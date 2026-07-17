using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Tests.Features.Installation;

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
