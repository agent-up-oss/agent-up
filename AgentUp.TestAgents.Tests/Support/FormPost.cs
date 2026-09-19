namespace AgentUp.TestAgents.Tests.Support;

/// <summary>
/// Posts a form, owning the request content.
/// <para>
/// Every one of these is a call to the provider's control plane or its token endpoint, and none
/// of them wants to keep a <c>using</c> for something it only sends, so the ownership lives here
/// once rather than at each call site.
/// </para>
/// </summary>
internal static class FormPost
{
    public static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string url,
        params (string Name, string Value)[] fields) =>
        SendAsync(client, url, CancellationToken.None, fields);

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken,
        params (string Name, string Value)[] fields)
    {
        using var content = new FormUrlEncodedContent(
            fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));
        return await client.PostAsync(url, content, cancellationToken);
    }
}
