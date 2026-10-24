using AgentUp.AUDebug.Features.Validation.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeValidationWorkspaceFixture : IValidationWorkspaceFixture
{
    public List<string> Prepared { get; } = [];

    public string CreateDataDirectory()
        => Path.Join(Path.GetTempPath(), "fake-validate-data", Guid.NewGuid().ToString("N"));

    public Dictionary<string, string> Environment { get; } = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> ServerEnvironment(string dataDirectory)
        => Environment;

    public Task PrepareAsync(string serverUrl, CancellationToken cancellationToken)
    {
        Prepared.Add(serverUrl);
        return Task.CompletedTask;
    }
}
