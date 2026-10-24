namespace AgentUp.AUDebug.Features.Validation.Interfaces;

public interface IValidationWorkspaceFixture
{
    string CreateDataDirectory();

    IReadOnlyDictionary<string, string> ServerEnvironment(string dataDirectory);

    Task PrepareAsync(string serverUrl, CancellationToken cancellationToken);
}
