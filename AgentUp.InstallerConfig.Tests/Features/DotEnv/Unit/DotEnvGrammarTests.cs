namespace AgentUp.InstallerConfig.Tests.Features.DotEnv.Unit;

[TestFixture]
public sealed class DotEnvGrammarTests
{
    private const string Source = ".env";

    [Test]
    public void Parse_readsAKeyAndValue()
    {
        Assert.That(RepositoryDotEnv.Parse(["AGENTUP_ADMIN_PASSWORD=secret"], Source),
            Is.EqualTo(new Dictionary<string, string> { ["AGENTUP_ADMIN_PASSWORD"] = "secret" }));
    }

    [Test]
    public void Parse_returnsNothingForAnEmptyFile()
    {
        Assert.That(RepositoryDotEnv.Parse([], Source), Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("# a comment")]
    [TestCase("   # an indented comment")]
    public void Parse_skipsBlankAndCommentLines(string line)
    {
        Assert.That(RepositoryDotEnv.Parse([line], Source), Is.Empty);
    }

    [Test]
    public void Parse_acceptsTheShellExportPrefixSoAFileCanBeSourced()
    {
        Assert.That(RepositoryDotEnv.Parse(["export AGENTUP_PORT=5000"], Source)["AGENTUP_PORT"],
            Is.EqualTo("5000"));
    }

    [Test]
    public void Parse_trimsSurroundingWhitespaceFromKeysAndValues()
    {
        Assert.That(RepositoryDotEnv.Parse(["  AGENTUP_PORT  =  5000  "], Source)["AGENTUP_PORT"],
            Is.EqualTo("5000"));
    }

    [TestCase("\"quoted value\"", "quoted value")]
    [TestCase("'quoted value'", "quoted value")]
    public void Parse_stripsMatchingQuotesSoASpaceCanBePartOfTheValue(string written, string expected)
    {
        Assert.That(RepositoryDotEnv.Parse([$"AGENTUP_NOTE={written}"], Source)["AGENTUP_NOTE"],
            Is.EqualTo(expected));
    }

    [TestCase("\"mismatched'")]
    [TestCase("\"unterminated")]
    [TestCase("trailing\"")]
    public void Parse_keepsQuotesThatDoNotSurroundTheWholeValue(string written)
    {
        Assert.That(RepositoryDotEnv.Parse([$"AGENTUP_NOTE={written}"], Source)["AGENTUP_NOTE"],
            Is.EqualTo(written));
    }

    [Test]
    public void Parse_readsAnEmptyValueAsEmptyRatherThanRejectingTheLine()
    {
        Assert.That(RepositoryDotEnv.Parse(["AGENTUP_NOTE="], Source)["AGENTUP_NOTE"],
            Is.EqualTo(string.Empty));
    }

    [Test]
    public void Parse_keepsEqualsSignsInsideTheValue()
    {
        Assert.That(RepositoryDotEnv.Parse(["AGENTUP_TOKEN=a=b=c"], Source)["AGENTUP_TOKEN"],
            Is.EqualTo("a=b=c"));
    }

    [Test]
    public void Parse_letsALaterEntryWinOverAnEarlierOne()
    {
        Assert.That(RepositoryDotEnv.Parse(["AGENTUP_PORT=5000", "AGENTUP_PORT=5100"], Source)["AGENTUP_PORT"],
            Is.EqualTo("5100"));
    }

    [TestCase("no-equals-sign")]
    [TestCase("=leading-equals")]
    public void Parse_rejectsALineThatIsNotAnAssignment(string line)
    {
        Assert.That(() => RepositoryDotEnv.Parse([line], Source),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [TestCase("1STARTS_WITH_DIGIT")]
    [TestCase("HAS-A-DASH")]
    [TestCase("HAS SPACE")]
    [TestCase("HAS.DOT")]
    public void Parse_rejectsANameAShellCouldNotExport(string key)
    {
        Assert.That(() => RepositoryDotEnv.Parse([$"{key}=value"], Source),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [TestCase("_LEADING_UNDERSCORE")]
    [TestCase("AGENTUP_PORT2")]
    [TestCase("lowercase_name")]
    public void Parse_acceptsEveryNameAShellWouldAccept(string key)
    {
        Assert.That(RepositoryDotEnv.Parse([$"{key}=value"], Source), Contains.Key(key));
    }

    [Test]
    public void Parse_namesTheFileAndLineSoABadEntryCanBeFound()
    {
        Assert.That(() => RepositoryDotEnv.Parse(["GOOD=1", "", "# comment", "bad line"], "/repo/.env"),
            Throws.InstanceOf<InvalidOperationException>()
                .With.Message.Contains("/repo/.env").And.Message.Contains("line 4"));
    }

    [Test]
    public void Parse_countsCommentAndBlankLinesWhenReportingAnInvalidName()
    {
        Assert.That(() => RepositoryDotEnv.Parse(["# comment", "BAD-NAME=1"], "/repo/.env"),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("line 2"));
    }
}
