using AgentUp.AUDebug.Features.Desktop.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeWorkspaceClient : IDesktopWorkspaceClient
{
    public string? Name { get; private set; }
    public string? Password { get; private set; }
    public Exception? Error { get; set; }

    public Task StartByNameAsync(string workspaceName, string password, CancellationToken cancellationToken)
    {
        Name = workspaceName;
        Password = password;
        cancellationToken.ThrowIfCancellationRequested();
        if (Error is not null)
            throw Error;
        return Task.CompletedTask;
    }
}
