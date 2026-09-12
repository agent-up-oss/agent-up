using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Providers;

namespace AgentUp.Verification.Tests.Features.Coverage.Provider;

[TestFixture]
public sealed class CoberturaReportParserTests
{
    private const string Root = "/home/user/agent-up";

    private static string Report(string source, string filename, string lines) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.9" version="1.9">
          <sources><source>{source}</source></sources>
          <packages>
            <package name="AgentUp.Server">
              <classes>
                <class name="AgentUp.Server.Thing" filename="{filename}">
                  <lines>{lines}</lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    [Test]
    public void Parse_resolvesAFilenameThatCarriesTheAbsolutePathMinusItsLeadingSlash()
    {
        // How coverlet writes it on Linux: source "/" plus a root-relative filename.
        var xml = Report(
            "/",
            "home/user/agent-up/AgentUp.Server/Program.cs",
            """<line number="7" hits="3" />""");

        var parsed = new CoberturaReportParser().Parse(xml, Root);

        Assert.That(parsed.Single().Path, Is.EqualTo("AgentUp.Server/Program.cs"));
    }

    [Test]
    public void Parse_resolvesAFilenameRelativeToARepositoryRootSource()
    {
        var xml = Report(Root, "AgentUp.Server/Program.cs", """<line number="7" hits="3" />""");

        var parsed = new CoberturaReportParser().Parse(xml, Root);

        Assert.That(parsed.Single().Path, Is.EqualTo("AgentUp.Server/Program.cs"));
    }

    [Test]
    public void Parse_dropsFilesOutsideTheRepository()
    {
        var xml = Report("/", "nuget/packages/Some.Package/Thing.cs", """<line number="1" hits="1" />""");

        Assert.That(new CoberturaReportParser().Parse(xml, Root), Is.Empty);
    }

    [Test]
    public void Parse_readsHitsAsCoveredAndZeroHitsAsCoverableButUncovered()
    {
        var xml = Report(
            "/",
            "home/user/agent-up/AgentUp.Server/Program.cs",
            """<line number="7" hits="3" /><line number="8" hits="0" />""");

        var file = new CoberturaReportParser().Parse(xml, Root).Single();

        Assert.Multiple(() =>
        {
            Assert.That(file.IsCovered(7), Is.True);
            Assert.That(file.IsCoverable(8), Is.True);
            Assert.That(file.IsCovered(8), Is.False);
            Assert.That(file.IsCoverable(9), Is.False, "A line absent from the report is not coverable.");
        });
    }

    [Test]
    public void Parse_takesTheHighestHitCountWhenALineAppearsTwice()
    {
        var xml = Report(
            "/",
            "home/user/agent-up/AgentUp.Server/Program.cs",
            """<line number="7" hits="0" /><line number="7" hits="4" />""");

        Assert.That(new CoberturaReportParser().Parse(xml, Root).Single().IsCovered(7), Is.True);
    }

    [Test]
    public void Parse_readsClassLevelLinesRatherThanDoubleCountingMethodLevelOnes()
    {
        var xml = $"""
            <coverage>
              <sources><source>/</source></sources>
              <packages><package name="P"><classes>
                <class name="C" filename="home/user/agent-up/AgentUp.Server/Program.cs">
                  <methods>
                    <method name="M"><lines><line number="7" hits="1" /></lines></method>
                  </methods>
                  <lines><line number="7" hits="1" /><line number="8" hits="0" /></lines>
                </class>
              </classes></package></packages>
            </coverage>
            """;

        var file = new CoberturaReportParser().Parse(xml, Root).Single();

        Assert.That(file.LineHits, Has.Count.EqualTo(2));
    }

    [Test]
    public void Parse_skipsAClassWithNoLines()
    {
        var xml = Report("/", "home/user/agent-up/AgentUp.Server/Program.cs", "");

        Assert.That(new CoberturaReportParser().Parse(xml, Root), Is.Empty);
    }

    [Test]
    public void Parse_throwsOnMalformedXmlRatherThanReportingNoCoverage()
    {
        Assert.That(() => new CoberturaReportParser().Parse("<coverage><unclosed>", Root),
            Throws.TypeOf<CoverageConfigurationException>());
    }
}
