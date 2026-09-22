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

    /// <summary>
    /// The capture copies from unmanaged memory, so an image smaller than the desktop it is
    /// supposed to hold has to be refused rather than read past its end.
    /// </summary>
    [TestCase(1279, 720, TestName = "RequireCoversRequestedArea_refuses_a_narrower_framebuffer")]
    [TestCase(1280, 719, TestName = "RequireCoversRequestedArea_refuses_a_shorter_framebuffer")]
    public void RequireCoversRequestedArea_refuses_a_framebuffer_smaller_than_the_desktop(int width, int height)
    {
        Assert.That(
            () => LinuxX11DesktopDisplayProvider.RequireCoversRequestedArea(
                Image(width, height, bytesPerLine: width * 4), 1280, 720),
            Throws.InvalidOperationException.With.Message.Contains("smaller than"));
    }

    [Test]
    public void RequireCoversRequestedArea_refuses_a_stride_too_short_for_one_row()
    {
        Assert.That(
            () => LinuxX11DesktopDisplayProvider.RequireCoversRequestedArea(
                Image(1280, 720, bytesPerLine: 1280), 1280, 720),
            Throws.InvalidOperationException.With.Message.Contains("stride"));
    }

    [Test]
    public void RequireCoversRequestedArea_refuses_an_image_with_no_pixel_data()
    {
        var image = Image(1280, 720, bytesPerLine: 1280 * 4) with { Data = IntPtr.Zero };

        Assert.That(
            () => LinuxX11DesktopDisplayProvider.RequireCoversRequestedArea(image, 1280, 720),
            Throws.InvalidOperationException.With.Message.Contains("no pixel data"));
    }

    // A framebuffer larger than the desktop is normal: the copy uses the image's own geometry.
    [Test]
    public void RequireCoversRequestedArea_accepts_a_framebuffer_at_least_as_large_as_the_desktop()
    {
        Assert.That(
            () => LinuxX11DesktopDisplayProvider.RequireCoversRequestedArea(
                Image(1920, 1080, bytesPerLine: 1920 * 4), 1280, 720),
            Throws.Nothing);
    }

    private static XImageData Image(int width, int height, int bytesPerLine) => new()
    {
        Width = width,
        Height = height,
        BytesPerLine = bytesPerLine,
        BitsPerPixel = 32,
        Data = new IntPtr(1),
    };
}
