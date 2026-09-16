using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.TestAgents.Features.IdentityProvider.Controllers;
using AgentUp.TestAgents.Features.IdentityProvider.Services;

namespace AgentUp.TestAgents.Tests.Features.IdentityProvider.Controller;

// The provider runs as its own process for the lifetime of a test run, so what matters at this
// boundary is that it reports the port it actually bound and that stopping it actually stops it.
[TestFixture, CancelAfter(120_000)]
public sealed class IdentityProviderControllerTests
{
    [Test]
    public async Task ServeAsync_reportsThePortItBoundAndAnswersOnIt()
    {
        var controller = new IdentityProviderController(new IdentityProviderHostService());
        var listening = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Port 0 asks for an ephemeral one, which is what keeps parallel runs from colliding. The
        // callback is the only way a caller learns which one it got.
        var serving = controller.ServeAsync(0, null, port => listening.TrySetResult(port), lifetime.Token);
        var boundPort = await listening.Task.WaitAsync(TimeSpan.FromSeconds(30));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var health = await client.GetAsync($"http://localhost:{boundPort}/health", lifetime.Token);
        var payload = await health.Content.ReadFromJsonAsync<JsonElement>(lifetime.Token);

        await lifetime.CancelAsync();
        await serving;

        Assert.Multiple(() =>
        {
            Assert.That(boundPort, Is.GreaterThan(0));
            Assert.That(health.IsSuccessStatusCode, Is.True);
            Assert.That(payload.GetProperty("status").GetString(), Is.EqualTo("ok"));
        });
    }

    // Cancellation has to return rather than throw: the host treats it as an ordinary shutdown and
    // exits zero, which is what a harness tearing the stack down expects.
    [Test]
    public async Task ServeAsync_returnsWhenItIsStoppedAndReleasesThePort()
    {
        var controller = new IdentityProviderController(new IdentityProviderHostService());
        var listening = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var serving = controller.ServeAsync(0, null, port => listening.TrySetResult(port), lifetime.Token);
        var boundPort = await listening.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await lifetime.CancelAsync();
        await serving.WaitAsync(TimeSpan.FromSeconds(30));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        Assert.That(
            async () => await client.GetAsync($"http://localhost:{boundPort}/health"),
            Throws.InstanceOf<HttpRequestException>().Or.InstanceOf<TaskCanceledException>(),
            "A stopped provider must stop answering, or a later test would reach a dead listener");
    }
}
