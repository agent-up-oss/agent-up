using AgentUp.AUDebug.Features.Desktop.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeDesktopWindowDriver : IDesktopWindowDriver
{
    public int Waits { get; private set; }
    public int Captures { get; private set; }
    public int Logins { get; private set; }
    public string? LastPassword { get; private set; }
    public string? LastCapturePath { get; private set; }
    public bool DelayUntilCanceled { get; set; }
    public bool WindowPresent { get; set; } = true;
    public Exception? CaptureException { get; set; }

    public async Task WaitForWindowAsync(CancellationToken cancellationToken)
    {
        Waits++;
        if (DelayUntilCanceled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public Task<bool> HasWindowAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(WindowPresent);
    }

    public async Task CaptureAsync(string outputPath, CancellationToken cancellationToken)
    {
        Captures++;
        LastCapturePath = outputPath;
        if (CaptureException is not null)
            throw CaptureException;
        if (DelayUntilCanceled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public Task LoginAsync(string password, CancellationToken cancellationToken)
    {
        Logins++;
        LastPassword = password;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
