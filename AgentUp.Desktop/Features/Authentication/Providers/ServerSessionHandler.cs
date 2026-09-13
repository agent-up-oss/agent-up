using System.Net.Http.Headers;

namespace AgentUp.Desktop.Features.Authentication.Providers;

public sealed class ServerSessionHandler : DelegatingHandler
{
    private Uri? _baseUri;
    private string? _accessToken;

    public ServerSessionHandler(HttpMessageHandler inner) : base(inner)
    {
    }

    public Uri? CurrentUri => _baseUri;

    public void Apply(Uri uri, string? accessToken)
    {
        _baseUri = uri;
        _accessToken = accessToken;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var session = _baseUri
            ?? throw new InvalidOperationException("The Desktop HTTP session has no Server URL.");
        request.RequestUri = Redirect(session, request.RequestUri);
        if (string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Authorization = null;
        else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        return base.SendAsync(request, cancellationToken);
    }

    private static Uri Redirect(Uri session, Uri? requestUri)
    {
        var origin = new Uri(session.GetLeftPart(UriPartial.Authority) + "/");
        if (requestUri is null)
            return origin;
        if (!requestUri.IsAbsoluteUri)
            return new Uri(origin, requestUri);

        return new Uri(origin, requestUri.PathAndQuery + requestUri.Fragment);
    }
}
