using AgentUp.Server.Features.TaskSource.Models;

namespace AgentUp.Server.Tests.Features.TaskSource.Unit;

[TestFixture]
public sealed class TaskSourceSettingTests
{
    [Test]
    public void ASourceNamesTheRepositoryAsOneString()
    {
        var source = new TaskSourceSetting("github", "agent-up-oss", "agent-up");

        Assert.Multiple(() =>
        {
            Assert.That(source.RepositoryKey, Is.EqualTo("agent-up-oss/agent-up"));
            Assert.That(source.IsComplete, Is.True);
        });
    }

    [TestCase("", "owner", "repo")]
    [TestCase("github", "", "repo")]
    [TestCase("github", "owner", "")]
    [TestCase("github", "owner", "   ")]
    public void ASourceMissingAnyPartIsNotSomethingThatCouldBeRead(string moduleId, string owner, string repository)
        => Assert.That(new TaskSourceSetting(moduleId, owner, repository).IsComplete, Is.False);
}
