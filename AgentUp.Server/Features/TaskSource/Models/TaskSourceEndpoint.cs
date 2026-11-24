namespace AgentUp.Server.Features.TaskSource.Models;

/// <summary>
/// The host URL a token is saved against. GitHub.com is https://github.com; a Forgejo instance
/// is the origin a person signs in at. The capability maps that origin to its REST API.
/// </summary>
public static class TaskSourceEndpoint
{
    public const string GithubDotCom = "https://github.com";

    public static bool TryNormalize(string? value, out string endpoint)
    {
        endpoint = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host))
            return false;

        var path = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        endpoint = path.Length > 0 ? path : uri.GetLeftPart(UriPartial.Authority);
        return endpoint.Length > 0;
    }
}
