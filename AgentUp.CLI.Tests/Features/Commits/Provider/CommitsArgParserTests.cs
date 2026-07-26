using AgentUp.CLI.Features.Commits.Providers;

namespace AgentUp.CLI.Tests.Features.Commits.Provider;

[TestFixture]
public sealed class CommitsArgParserTests
{
    [Test]
    public void Parse_allRequiredArgs_returnsRequest()
    {
        var parser = new CommitsArgParser();

        var (request, error) = parser.Parse(["--slice", "S", "--message", "msg", "--files", "a.cs"]);

        Assert.That(error, Is.Null);
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.Slice, Is.EqualTo("S"));
        Assert.That(request.Message, Is.EqualTo("msg"));
        Assert.That(request.Files, Is.EqualTo(new[] { "a.cs" }));
    }

    [Test]
    public void Parse_multipleFiles_collectsAll()
    {
        var parser = new CommitsArgParser();

        var (request, error) = parser.Parse(["--slice", "S", "--message", "msg", "--files", "a.cs", "b.cs", "c.cs"]);

        Assert.That(error, Is.Null);
        Assert.That(request!.Files, Is.EqualTo(new[] { "a.cs", "b.cs", "c.cs" }));
    }

    [Test]
    public void Parse_withTests_collectsTestCommands()
    {
        var parser = new CommitsArgParser();

        var (request, error) = parser.Parse(["--slice", "S", "--message", "msg", "--files", "a.cs", "--tests", "cmd1", "cmd2"]);

        Assert.That(error, Is.Null);
        Assert.That(request!.Tests, Is.EqualTo(new[] { "cmd1", "cmd2" }));
    }

    [Test]
    public void Parse_missingSlice_returnsError()
    {
        var parser = new CommitsArgParser();

        var (request, error) = parser.Parse(["--message", "msg", "--files", "a.cs"]);

        Assert.That(request, Is.Null);
        Assert.That(error, Does.Contain("--slice"));
    }

    [Test]
    public void Parse_missingMessage_returnsError()
    {
        var parser = new CommitsArgParser();

        var (request, error) = parser.Parse(["--slice", "S", "--files", "a.cs"]);

        Assert.That(request, Is.Null);
        Assert.That(error, Does.Contain("--message"));
    }

    [Test]
    public void Parse_missingFiles_returnsError()
    {
        var parser = new CommitsArgParser();

        var (request, error) = parser.Parse(["--slice", "S", "--message", "msg"]);

        Assert.That(request, Is.Null);
        Assert.That(error, Does.Contain("--files"));
    }

    [Test]
    public void Parse_unknownFlag_returnsError()
    {
        var parser = new CommitsArgParser();

        var (request, error) = parser.Parse(["--unknown", "val"]);

        Assert.That(request, Is.Null);
        Assert.That(error, Does.Contain("Unknown argument"));
    }

    [Test]
    public void Parse_filesStopAtNextFlag_doesNotConsumeNextFlagAsFile()
    {
        var parser = new CommitsArgParser();

        var (request, _) = parser.Parse(["--slice", "S", "--message", "msg", "--files", "a.cs", "--tests", "cmd"]);

        Assert.That(request!.Files, Is.EqualTo(new[] { "a.cs" }));
        Assert.That(request.Tests, Is.EqualTo(new[] { "cmd" }));
    }
}
