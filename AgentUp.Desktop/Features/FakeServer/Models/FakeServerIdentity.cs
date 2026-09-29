namespace AgentUp.Desktop.Features.FakeServer.Models;

public static class FakeServerIdentity
{
    public const string Id = "fake";
    public const string Url = "http://127.0.0.1:9";
    public const string DisplayName = "Demo";

    public static bool Matches(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && string.Equals(Normalize(url), Url, StringComparison.OrdinalIgnoreCase);

    public static bool Matches(Uri? uri)
        => uri is not null && Matches(uri.GetLeftPart(UriPartial.Authority).TrimEnd('/'));

    public static string Normalize(string url)
        => url.Trim().TrimEnd('/');
}
