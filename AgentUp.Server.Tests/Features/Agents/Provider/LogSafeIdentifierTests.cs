using AgentUp.Server.Shared.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class LogSafeIdentifierTests
{
    /// <summary>
    /// A workspace id arrives from a REST route, and a rendered log line is one line of text. An
    /// id carrying a newline would otherwise write a second line that reads like a real entry.
    /// </summary>
    [Test]
    public void Of_replaces_the_newlines_a_forged_identifier_would_use()
    {
        var forged = LogSafeIdentifier.Of("ws-1\nWARN  Agent for workspace ws-2 exited.");

        Assert.Multiple(() =>
        {
            Assert.That(forged, Does.Not.Contain("\n"));
            Assert.That(forged, Does.Not.Contain("\r"));
            Assert.That(forged, Does.StartWith("ws-1"));
            Assert.That(forged, Does.Contain("Agent for workspace ws-2 exited."),
                "the forged text stays visible on the line it tried to escape");
        });
    }

    [TestCase("\r")]
    [TestCase("\t")]
    [TestCase("\u0000")]
    [TestCase("\u001b")]
    public void Of_replaces_every_control_character(string control)
    {
        Assert.That(LogSafeIdentifier.Of("ws-1" + control + "2"), Is.EqualTo("ws-1�2"));
    }

    [Test]
    public void Of_leaves_an_ordinary_identifier_alone()
    {
        Assert.That(LogSafeIdentifier.Of("c3717647-cb23-4c63-a3fb-60af3a5e8120"),
            Is.EqualTo("c3717647-cb23-4c63-a3fb-60af3a5e8120"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Of_reads_a_missing_identifier_as_empty(string? value)
    {
        Assert.That(LogSafeIdentifier.Of(value), Is.Empty);
    }

    // A log line is not a place to reproduce an unbounded request value.
    [Test]
    public void Of_bounds_the_length_it_will_write()
    {
        Assert.That(LogSafeIdentifier.Of(new string('a', 5_000)), Has.Length.EqualTo(200));
    }
}
