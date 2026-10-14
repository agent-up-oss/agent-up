using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class LoopScenarioCatalogTests
{
    [Test]
    public void Load_readsEveryJsonScenarioInTheDirectory()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-loop-scenarios").FullName;
        try
        {
            File.WriteAllText(Path.Join(root, "a.json"),
                """{"id":"a","prompt":"do a","scoresOutcome":true,"expectTool":"complete_task","expectCompleted":true}""");
            File.WriteAllText(Path.Join(root, "b.json"),
                """{"id":"b","prompt":"do b","scoresOutcome":false,"expectCompleted":false}""");

            var loaded = new LoopScenarioCatalog().Load(root);

            Assert.Multiple(() =>
            {
                Assert.That(loaded, Has.Count.EqualTo(2));
                Assert.That(loaded[0].Id, Is.EqualTo("a"));
                Assert.That(loaded[0].ExpectTool, Is.EqualTo("complete_task"));
                Assert.That(loaded[0].ExpectCompleted, Is.True);
                Assert.That(loaded[1].ScoresOutcome, Is.False);
                Assert.That(loaded[1].ExpectCompleted, Is.False);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Load_failsWhenTheDirectoryIsMissing()
    {
        Assert.That(
            () => new LoopScenarioCatalog().Load("/definitely-missing-loop-coverage"),
            Throws.InvalidOperationException.With.Message.Contains("were not found"));
    }
}
