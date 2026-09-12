using System.Net;

namespace AgentUp.Tray.Tests.Features.Tray.Fake;

/// <summary>
/// Answers the tray's requests in memory and records each one, so the poll, restart and
/// shutdown paths can be asserted without a server listening.
/// </summary>
public sealed class FakeHttpExchange : HttpMessageHandler
{
    private readonly Func<Task<HttpResponseMessage>> _respond;
    private readonly List<string> _requests = [];
    private readonly TaskCompletionSource _firstRequest =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private FakeHttpExchange(Func<Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public static FakeHttpExchange Answering(HttpStatusCode status)
        => new(() => Task.FromResult(new HttpResponseMessage(status)));

    /// <summary>An exchange that fails the way an absent or closing server does.</summary>
    public static FakeHttpExchange Failing(Exception error)
        => new(() => Task.FromException<HttpResponseMessage>(error));

    /// <summary>Completes once a request has been recorded, so tests need no polling.</summary>
    public Task FirstRequest => _firstRequest.Task;

    public IReadOnlyList<string> Requests
    {
        get { lock (_requests) return _requests.ToArray(); }
    }

    /// <summary>Builds a client the manager accepts: relative paths need a base address.</summary>
    public HttpClient AsClient() => new(this) { BaseAddress = new Uri("http://tray.invalid") };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        lock (_requests)
            _requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");

        // Recorded before responding so a failing exchange still reports what was asked for.
        _firstRequest.TrySetResult();

        return _respond();
    }
}
