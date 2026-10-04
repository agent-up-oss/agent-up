using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Mobile.Provider;

[TestFixture]
public sealed class ChromiumCdpMessageProviderTests
{
    [Test]
    public void Evaluate_asksRuntimeToAwaitTheExpression()
    {
        var json = ChromiumCdpMessageProvider.Evaluate("void 0");

        Assert.That(json, Does.Contain("\"Runtime.evaluate\""));
        Assert.That(json, Does.Contain("\"awaitPromise\":true"));
        Assert.That(json, Does.Contain("void 0"));
        Assert.That(ChromiumCdpMessageProvider.Evaluate("void 0", 9), Does.Contain("\"id\":9"));
    }

    [Test]
    public void CaptureScreenshot_asksForAPngFromTheSurface()
    {
        var json = ChromiumCdpMessageProvider.CaptureScreenshot();

        Assert.That(json, Does.Contain("\"Page.captureScreenshot\""));
        Assert.That(json, Does.Contain("\"png\""));
        Assert.That(json, Does.Contain("\"fromSurface\":true"));
        Assert.That(json, Does.Not.Contain("captureBeyondViewport"));
    }

    [Test]
    public void CaptureScreenshot_canAskForTheWholeDocument()
    {
        var json = ChromiumCdpMessageProvider.CaptureScreenshot(4, true);

        Assert.That(json, Does.Contain("\"id\":4"));
        Assert.That(json, Does.Contain("\"captureBeyondViewport\":true"));
    }

    [Test]
    public void SetDeviceMetrics_overridesTheEmulatedViewport()
    {
        var json = ChromiumCdpMessageProvider.SetDeviceMetrics(1440, 3200, 3);

        Assert.That(json, Does.Contain("\"Emulation.setDeviceMetricsOverride\""));
        Assert.That(json, Does.Contain("\"width\":1440"));
        Assert.That(json, Does.Contain("\"height\":3200"));
        Assert.That(json, Does.Contain("\"id\":3"));
        Assert.That(json, Does.Contain("\"mobile\":false"));
    }

    [Test]
    public void MobileDeviceMetrics_marksTheEmulationAsAPhone()
    {
        var json = ChromiumCdpMessageProvider.MobileDeviceMetrics(390, 844, 7);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"Emulation.setDeviceMetricsOverride\""));
            Assert.That(json, Does.Contain("\"width\":390"));
            Assert.That(json, Does.Contain("\"height\":844"));
            Assert.That(json, Does.Contain("\"mobile\":true"));
            Assert.That(json, Does.Contain("\"id\":7"));
        });
    }

    [Test]
    public void Navigate_loadsTheGivenUrl()
    {
        var json = ChromiumCdpMessageProvider.Navigate("http://127.0.0.1:9/connect", 4);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"Page.navigate\""));
            Assert.That(json, Does.Contain("http://127.0.0.1:9/connect"));
            Assert.That(json, Does.Contain("\"id\":4"));
        });
    }

    [Test]
    public void MouseEvent_releasesTheButtonOnMouseReleased()
    {
        var pressed = ChromiumCdpMessageProvider.MouseEvent("mousePressed", 12, 34, 5);
        var released = ChromiumCdpMessageProvider.MouseEvent("mouseReleased", 12, 34, 6);

        Assert.Multiple(() =>
        {
            Assert.That(pressed, Does.Contain("\"Input.dispatchMouseEvent\""));
            Assert.That(pressed, Does.Contain("\"buttons\":1"));
            Assert.That(released, Does.Contain("\"type\":\"mouseReleased\""));
            Assert.That(released, Does.Contain("\"buttons\":0"));
        });
    }

    [Test]
    public void InsertText_typesTheValueIntoTheFocusedField()
    {
        var json = ChromiumCdpMessageProvider.InsertText("hello", 8);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"Input.insertText\""));
            Assert.That(json, Does.Contain("hello"));
            Assert.That(json, Does.Contain("\"id\":8"));
        });
    }

    [Test]
    public void ReadPoint_returnsCoordinatesWhenTheElementIsThere()
    {
        var point = ChromiumCdpMessageProvider.ReadPoint(
            """{"id":1,"result":{"result":{"type":"object","value":{"x":12,"y":34}}}}""",
            "locate Git");

        Assert.That(point, Is.EqualTo((12, 34)));
    }

    [Test]
    public void ReadPoint_isNullWhileTheElementIsMissing()
    {
        Assert.That(
            ChromiumCdpMessageProvider.ReadPoint(
                """{"id":1,"result":{"result":{"type":"object","value":null}}}""",
                "locate Git"),
            Is.Null);
    }

    [Test]
    public void ReadPoint_isNullWhenTheValueHasNoCoordinates()
    {
        Assert.That(
            ChromiumCdpMessageProvider.ReadPoint(
                """{"id":1,"result":{"result":{"type":"object","value":{"label":"Git"}}}}""",
                "locate Git"),
            Is.Null);
    }

    [Test]
    public void ReadPoint_surfacesACdpError()
    {
        Assert.That(
            () => ChromiumCdpMessageProvider.ReadPoint(
                """{"id":1,"error":{"message":"context gone"}}""",
                "locate Git"),
            Throws.InvalidOperationException.With.Message.Contains("locate Git CDP failed"));
    }

    [Test]
    public void ReadSize_readsWidthAndHeight()
    {
        var size = ChromiumCdpMessageProvider.ReadSize(
            """{"id":1,"result":{"result":{"type":"object","value":{"width":1440,"height":2800}}}}""");

        Assert.That(size.Width, Is.EqualTo(1440));
        Assert.That(size.Height, Is.EqualTo(2800));
    }

    [Test]
    public void ReadSize_requiresWidthAndHeight()
    {
        Assert.That(
            () => ChromiumCdpMessageProvider.ReadSize("""{"id":1,"result":{"result":{"type":"object","value":{}}}}"""),
            Throws.InvalidOperationException.With.Message.Contains("width and height"));
    }

    [Test]
    public void ThrowIfEvaluateFailed_acceptsASuccessfulResult()
    {
        Assert.DoesNotThrow(() => ChromiumCdpMessageProvider.ThrowIfEvaluateFailed(
            """{"id":1,"result":{"result":{"type":"string","value":"ok"}}}""",
            "Mobile login"));
    }

    [Test]
    public void ThrowIfEvaluateFailed_surfacesACdpError()
    {
        Assert.That(
            () => ChromiumCdpMessageProvider.ThrowIfEvaluateFailed(
                """{"id":1,"error":{"code":-32000,"message":"Execution context was destroyed."}}""",
                "Mobile login"),
            Throws.InvalidOperationException.With.Message.Contains("Mobile login CDP failed")
                .And.Message.Contains("Execution context was destroyed"));
    }

    [Test]
    public void ThrowIfEvaluateFailed_surfacesPageExceptionDetails()
    {
        Assert.That(
            () => ChromiumCdpMessageProvider.ThrowIfEvaluateFailed(
                """{"id":1,"result":{"exceptionDetails":{"text":"button missing"}}}""",
                "Mobile open-agent"),
            Throws.InvalidOperationException.With.Message.EqualTo("Mobile open-agent failed: {\"text\":\"button missing\"}"));
    }

    [Test]
    public void ReadPng_decodesTheScreenshotData()
    {
        var png = Convert.ToBase64String([137, 80, 78, 71]);
        var bytes = ChromiumCdpMessageProvider.ReadPng("{\"id\":2,\"result\":{\"data\":\"" + png + "\"}}");

        Assert.That(bytes, Is.EqualTo(new byte[] { 137, 80, 78, 71 }));
    }

    [Test]
    public void ReadPng_surfacesACdpError()
    {
        Assert.That(
            () => ChromiumCdpMessageProvider.ReadPng("""{"id":2,"error":{"message":"No frame"}}"""),
            Throws.InvalidOperationException.With.Message.Contains("Mobile open-agent screenshot failed"));
    }

    [Test]
    public void ReadPng_requiresImageData()
    {
        Assert.That(
            () => ChromiumCdpMessageProvider.ReadPng("""{"id":2,"result":{}}"""),
            Throws.InvalidOperationException.With.Message.EqualTo("Mobile open-agent screenshot did not return an image."));
    }

    [Test]
    public void ReadPng_rejectsEmptyImageData()
    {
        Assert.That(
            () => ChromiumCdpMessageProvider.ReadPng("""{"id":2,"result":{"data":""}}"""),
            Throws.InvalidOperationException.With.Message.EqualTo("Mobile open-agent screenshot did not return an image."));
    }
}
