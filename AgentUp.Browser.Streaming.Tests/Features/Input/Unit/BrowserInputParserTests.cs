using AgentUp.Browser.Streaming.Models;
using AgentUp.Browser.Streaming.Tests.Support;
using PuppeteerSharp.Input;

namespace AgentUp.Browser.Streaming.Tests.Features.Input.Unit;

[TestFixture]
public sealed class BrowserInputParserTests
{
    private static BrowserInputCommand Parse(BrowserInputJson json)
        => new BrowserInputParser().Parse(json.Build());

    [TestCase("mousemove", BrowserInputKind.MouseMove)]
    [TestCase("mousedown", BrowserInputKind.MouseDown)]
    [TestCase("mouseup", BrowserInputKind.MouseUp)]
    [TestCase("click", BrowserInputKind.Click)]
    [TestCase("wheel", BrowserInputKind.Wheel)]
    [TestCase("keydown", BrowserInputKind.KeyDown)]
    [TestCase("keyup", BrowserInputKind.KeyUp)]
    [TestCase("type", BrowserInputKind.Type)]
    [TestCase("controlmode", BrowserInputKind.ControlMode)]
    public void Parse_recognisesEveryHandledInputType(string type, BrowserInputKind expected)
    {
        var json = BrowserInputJson.OfType(type).At(10, 20)
            .With("deltaX", 1).With("deltaY", 2).With("key", "a").With("text", "hi");

        Assert.That(Parse(json).Kind, Is.EqualTo(expected));
    }

    [Test]
    public void Parse_reportsAnUnhandledTypeAsUnknownButKeepsIt()
    {
        // A present-but-unhandled type still counts as input activity, so the raw value
        // has to survive decoding.
        var command = Parse(BrowserInputJson.OfType("doubletap"));

        Assert.Multiple(() =>
        {
            Assert.That(command.Kind, Is.EqualTo(BrowserInputKind.Unknown));
            Assert.That(command.Type, Is.EqualTo("doubletap"));
        });
    }

    [Test]
    public void Parse_reportsANullTypeAsUnknownWithNoActivity()
    {
        var command = Parse(BrowserInputJson.OfType(null!).With("type", null));

        Assert.Multiple(() =>
        {
            Assert.That(command.Kind, Is.EqualTo(BrowserInputKind.Unknown));
            Assert.That(command.Type, Is.Null, "A null type must not register input activity.");
        });
    }

    [Test]
    public void Parse_throwsWhenTheMessageHasNoType()
    {
        // The dispatcher relies on catching this to drop a malformed message rather than
        // acting on half of it.
        Assert.That(() => Parse(BrowserInputJson.Untyped()),
            Throws.TypeOf<KeyNotFoundException>());
    }

    [Test]
    public void Parse_readsCoordinatesForPointerInput()
    {
        var command = Parse(BrowserInputJson.OfType("click").At(12.5, 34.25));

        Assert.Multiple(() =>
        {
            Assert.That(command.X, Is.EqualTo(12.5m));
            Assert.That(command.Y, Is.EqualTo(34.25m));
        });
    }

    [Test]
    public void Parse_throwsWhenPointerInputOmitsACoordinate()
    {
        Assert.That(() => Parse(BrowserInputJson.OfType("mousemove").With("x", 1)),
            Throws.TypeOf<KeyNotFoundException>());
    }

    [Test]
    public void Parse_doesNotRequireCoordinatesForKeyboardInput()
    {
        // A keystroke carries no x/y. Demanding one would drop every keypress.
        var command = Parse(BrowserInputJson.OfType("keydown").With("key", "Enter"));

        Assert.Multiple(() =>
        {
            Assert.That(command.Key, Is.EqualTo("Enter"));
            Assert.That(command.X, Is.Zero);
        });
    }

    [Test]
    public void Parse_readsWheelDeltas()
    {
        var command = Parse(BrowserInputJson.OfType("wheel").With("deltaX", -3.5).With("deltaY", 120));

        Assert.Multiple(() =>
        {
            Assert.That(command.DeltaX, Is.EqualTo(-3.5m));
            Assert.That(command.DeltaY, Is.EqualTo(120m));
        });
    }

    [Test]
    public void Parse_readsTypedText()
    {
        Assert.That(Parse(BrowserInputJson.OfType("type").With("text", "hello")).Text, Is.EqualTo("hello"));
    }

    [Test]
    public void Parse_treatsANullKeyOrTextAsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse(BrowserInputJson.OfType("keydown").With("key", null)).Key, Is.Empty);
            Assert.That(Parse(BrowserInputJson.OfType("type").With("text", null)).Text, Is.Empty);
        });
    }

    [TestCase("middle", MouseButton.Middle)]
    [TestCase("right", MouseButton.Right)]
    [TestCase("left", MouseButton.Left)]
    [TestCase("unrecognised", MouseButton.Left)]
    public void Parse_mapsTheMouseButtonAndDefaultsToLeft(string button, MouseButton expected)
    {
        var command = Parse(BrowserInputJson.OfType("mousedown").At(1, 1).With("button", button));

        Assert.That(command.Button, Is.EqualTo(expected));
    }

    [Test]
    public void Parse_defaultsTheMouseButtonWhenAbsent()
    {
        Assert.That(Parse(BrowserInputJson.OfType("mousedown").At(1, 1)).Button, Is.EqualTo(MouseButton.Left));
    }

    [Test]
    public void Parse_defaultsClickCountToOne()
    {
        Assert.That(Parse(BrowserInputJson.OfType("click").At(1, 1)).ClickCount, Is.EqualTo(1));
    }

    [Test]
    public void Parse_readsAnExplicitClickCount()
    {
        Assert.That(Parse(BrowserInputJson.OfType("click").At(1, 1).With("clickCount", 2)).ClickCount, Is.EqualTo(2));
    }

    [Test]
    public void ClickOptions_carriesTheButtonAndCount()
    {
        var options = Parse(BrowserInputJson.OfType("click").At(1, 1)
            .With("button", "right").With("clickCount", 3)).ClickOptions;

        Assert.Multiple(() =>
        {
            Assert.That(options.Button, Is.EqualTo(MouseButton.Right));
            Assert.That(options.Count, Is.EqualTo(3));
        });
    }

    [Test]
    public void Parse_readsAnExplicitViewportOnControlMode()
    {
        var command = Parse(BrowserInputJson.OfType("controlmode").With("width", 1280).With("height", 720));

        Assert.Multiple(() =>
        {
            Assert.That(command.HasViewport, Is.True);
            Assert.That(command.Width, Is.EqualTo(1280));
            Assert.That(command.Height, Is.EqualTo(720));
        });
    }

    [Test]
    public void Parse_reportsNoViewportWhenControlModeOmitsOneDimension()
    {
        var command = Parse(BrowserInputJson.OfType("controlmode").With("width", 1280));

        Assert.Multiple(() =>
        {
            Assert.That(command.HasViewport, Is.False);
            Assert.That(command.Height, Is.Null);
        });
    }

    [Test]
    public void Parse_ignoresANonNumericViewportDimension()
    {
        var command = Parse(BrowserInputJson.OfType("controlmode").With("width", "wide").With("height", 720));

        Assert.That(command.HasViewport, Is.False);
    }

    [Test]
    public void Parse_throwsOnMalformedJson()
    {
        Assert.That(() => new BrowserInputParser().Parse("{ not json"),
            Throws.InstanceOf<System.Text.Json.JsonException>());
    }
}
