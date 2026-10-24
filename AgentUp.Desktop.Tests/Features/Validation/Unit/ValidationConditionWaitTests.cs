using AgentUp.Desktop.Features.Validation.Models;

namespace AgentUp.Desktop.Tests.Features.Validation.Unit;

public sealed class ValidationConditionWaitTests
{
    [Test]
    public async Task UntilAsync_returnsNullWhenTheConditionPasses()
    {
        var error = await ValidationConditionWait.UntilAsync(
            _ => Task.FromResult(true),
            "enabled 'git.commit'",
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.That(error, Is.Null);
    }

    [Test]
    public async Task UntilAsync_namesTheConditionWhenTheDeadlineExpires()
    {
        var error = await ValidationConditionWait.UntilAsync(
            _ => Task.FromResult(false),
            "enabled 'git.commit'",
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.That(error, Is.EqualTo("Timed out waiting for enabled 'git.commit'."));
    }
}
