namespace AgentUp.Desktop.Features.Validation.Models;

public static class ValidationConditionWait
{
    public const int DefaultAttempts = 40;

    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    public static async Task<string?> UntilAsync(
        Func<CancellationToken, Task<bool>> condition,
        string name,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < DefaultAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await condition(cancellationToken))
                return null;

            await delay(PollInterval, cancellationToken);
        }

        return $"Timed out waiting for {name}.";
    }
}
