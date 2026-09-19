using AgentUp.CLI.Composition;
using AgentUp.Desktop.Composition;
using AgentUp.InstallerApp.Composition;
using AgentUp.Server.Composition;
using AgentUp.Tray.Composition;
using LocalInstaller.Core.Shared.Models;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// The property CI depends on: every payload lands in a directory of its own.
/// </summary>
/// <remarks>
/// This deliberately does not name the payloads. An exact set would fail on any legitimate
/// new one while verifying no behaviour, and the CI layout does not care what the
/// directories are called - only that two payloads never stage into the same one and that
/// none stages into the artifact root.
/// </remarks>
[TestFixture]
public sealed class ReleasePayloadLayout
{
    [Test]
    public void Every_AgentUp_payload_stages_into_a_directory_of_its_own()
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
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(payloadDirectories, Has.None.Null.Or.Empty,
                "A payload with no directory stages into the artifact root and collides with the rest.");
            Assert.That(payloadDirectories, Is.Unique,
                "Two payloads sharing a directory overwrite each other when CI stages them.");
        });
    }
}
