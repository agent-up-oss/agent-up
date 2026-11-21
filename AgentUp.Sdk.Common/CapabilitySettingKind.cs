namespace AgentUp.Sdk.Common;

public static class CapabilitySettingKind
{
    public const string Text = "text";
    public const string Url = "url";

    /// <summary>A value the Server stores but never returns to a client.</summary>
    public const string Secret = "secret";

    public static readonly IReadOnlyList<string> Allowed = [Text, Url, Secret];
}
