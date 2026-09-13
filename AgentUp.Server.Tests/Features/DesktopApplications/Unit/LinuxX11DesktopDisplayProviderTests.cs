using AgentUp.Server.Features.DesktopApplications.Models;
using AgentUp.Server.Features.DesktopApplications.Providers;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Unit;

[TestFixture]
public sealed class LinuxX11DesktopDisplayProviderTests
{
    [Test]
    public void NormalizeKey_mapsBrowserKeysOntoX11Names()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LinuxX11DesktopDisplayProvider.NormalizeKey(" "), Is.EqualTo("space"));
            Assert.That(LinuxX11DesktopDisplayProvider.NormalizeKey("ArrowUp"), Is.EqualTo("Up"));
            Assert.That(LinuxX11DesktopDisplayProvider.NormalizeKey("ArrowDown"), Is.EqualTo("Down"));
            Assert.That(LinuxX11DesktopDisplayProvider.NormalizeKey("ArrowLeft"), Is.EqualTo("Left"));
            Assert.That(LinuxX11DesktopDisplayProvider.NormalizeKey("ArrowRight"), Is.EqualTo("Right"));
            Assert.That(LinuxX11DesktopDisplayProvider.NormalizeKey("Escape"), Is.EqualTo("Escape"));
        });
    }

    [Test]
    public void StartAsync_rejectsWindowDimensionsOutsideTheSupportedRange()
    {
        var displays = new LinuxX11DesktopDisplayProvider(new PngFrameProvider());
        Assert.ThrowsAsync<InvalidOperationException>(() => displays.StartAsync(1, 1, CancellationToken.None));
    }


    [Test]
    public void ReadPixel_supportsLittleAndBigEndianFramebuffers()
    {
        var image = new XImageData { BytesPerLine = 4, BitsPerPixel = 32, ByteOrder = 0 };
        var little = LinuxX11DesktopDisplayProvider.ReadPixel([0x11, 0x22, 0x33, 0x44], image, 0, 0);
        image.ByteOrder = 1;
        var big = LinuxX11DesktopDisplayProvider.ReadPixel([0x11, 0x22, 0x33, 0x44], image, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(little, Is.EqualTo((nuint)0x44332211));
            Assert.That(big, Is.EqualTo((nuint)0x11223344));
        });
    }
}
