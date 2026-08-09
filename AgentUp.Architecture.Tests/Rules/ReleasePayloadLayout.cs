using AgentUp.CLI.Composition;
using AgentUp.Desktop.Composition;
using AgentUp.InstallerApp.Composition;
using AgentUp.Server.Composition;
using AgentUp.Tray.Composition;
using LocalInstaller.Core.Shared.Models;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class ReleasePayloadLayout
{
    [Test]
    public void AgentUp_payload_manifest_directories_match_dotnet_ci_artifact_layout()
    {
        LocalInstallerArtifactManifest[] manifests =
        [
            new AgentUpInstallerAppManifest(),
            new AgentUpDesktopManifest(),
            new AgentUpServerManifest(),
            new AgentUpCliManifest(),
            new AgentUpTrayManifest()
        ];

        var payloadDirectories = manifests
            .Select(manifest => manifest.PayloadDirectoryName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(payloadDirectories, Is.EqualTo(new[]
        {
            "cli",
            "desktop",
            "installer",
            "server",
            "tray"
        }));
    }
}
