using AgentUp.TestAgents.Features.Scripted.Providers;

namespace AgentUp.TestAgents.Tests.Features.Scripted.Provider;

[TestFixture]
public sealed class WorkingDirectoryFileWriterTests
{
    [Test]
    public async Task WriteAsync_writesUnderTheWorkingDirectoryAndRefusesAPathOutsideIt()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-scripted-files").FullName;
        var writer = new WorkingDirectoryFileWriter();

        var inside = await writer.WriteAsync(root, "web/greeting.txt", "Ahoy\n", CancellationToken.None);
        var outside = await writer.WriteAsync(root, "../escape.txt", "no", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(inside, Is.Null);
            Assert.That(File.ReadAllText(Path.Join(root, "web", "greeting.txt")), Is.EqualTo("Ahoy\n"));
            Assert.That(outside, Does.Contain("outside the working directory"));
            Assert.That(File.Exists(Path.Join(Path.GetDirectoryName(root)!, "escape.txt")), Is.False);
        });
        Directory.Delete(root, recursive: true);
    }
}
