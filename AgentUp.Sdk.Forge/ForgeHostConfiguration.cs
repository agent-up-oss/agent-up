namespace AgentUp.Sdk.Forge;

/// <summary>
/// Host endpoints a forge module advertises. API and OAuth URLs stay in the module so
/// Server orchestration never names a particular repository host.
/// </summary>
public sealed record ForgeHostConfiguration(
    string ApiBaseUrl,
    string OAuthAuthorizeUrl,
    string OAuthTokenUrl);
