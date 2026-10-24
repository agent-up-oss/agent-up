using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Providers;

namespace AgentUp.Desktop.Tests.Features.Validation.Provider;

public sealed class ValidationArtifactStoreTests
{
    [Test]
    public async Task WriteReportAsync_writesCamelCaseJsonUnderTheFlowDirectory()
    {
        var root = Path.Join(Path.GetTempPath(), "desktop-validation", Guid.NewGuid().ToString("N"));
        var store = new ValidationArtifactStore();
        var directory = store.FlowDirectory(root, "open git");

        await store.WriteReportAsync(
            directory,
            new ValidationRunReportDto(
                "open git",
                "Open Git",
                "Desktop",
                true,
                "passed",
                DateTimeOffset.UnixEpoch,
                12,
                []),
            CancellationToken.None);

        var json = await File.ReadAllTextAsync(Path.Join(directory, "report.json"));
        Assert.Multiple(() =>
        {
            Assert.That(directory, Does.EndWith("open_git"));
            Assert.That(json, Does.Contain("\"flowId\": \"open git\""));
            Assert.That(json, Does.Contain("\"passed\": true"));
        });
    }

    [Test]
    public async Task CopyLogAsync_copiesAnExistingServerLog()
    {
        var root = Path.Join(Path.GetTempPath(), "desktop-validation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Join(root, "server-source.log");
        await File.WriteAllTextAsync(source, "ready");
        var store = new ValidationArtifactStore();
        var directory = store.FlowDirectory(root, "flow");

        await store.CopyLogAsync(directory, "server.log", source, CancellationToken.None);

        Assert.That(await File.ReadAllTextAsync(Path.Join(directory, "server.log")), Is.EqualTo("ready"));
    }

    [Test]
    public async Task CopyLogAsync_skipsAMissingSource()
    {
        var root = Path.Join(Path.GetTempPath(), "desktop-validation", Guid.NewGuid().ToString("N"));
        var store = new ValidationArtifactStore();
        var directory = store.FlowDirectory(root, "flow");

        await store.CopyLogAsync(directory, "server.log", Path.Join(root, "missing.log"), CancellationToken.None);

        Assert.That(File.Exists(Path.Join(directory, "server.log")), Is.False);
    }
}
