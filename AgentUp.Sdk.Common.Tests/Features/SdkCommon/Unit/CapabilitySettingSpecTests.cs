using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Common.Tests.Features.SdkCommon.Unit;

[TestFixture]
public sealed class CapabilitySettingSpecTests
{
    private static readonly CapabilitySettingSpec Endpoint = new("endpoint", "Endpoint", CapabilitySettingKind.Url, Required: true);
    private static readonly CapabilitySettingSpec Token = new("token", "Token", CapabilitySettingKind.Secret);

    [Test]
    public void MissingRequired_names_a_required_setting_with_no_value()
    {
        var missing = CapabilitySettingSpec.MissingRequired([Endpoint, Token], new Dictionary<string, string>());

        Assert.That(missing, Is.EqualTo(new[] { "endpoint" }));
    }

    [Test]
    public void MissingRequired_treats_a_blank_value_as_missing()
    {
        var missing = CapabilitySettingSpec.MissingRequired([Endpoint], new Dictionary<string, string> { ["endpoint"] = "  " });

        Assert.That(missing, Is.EqualTo(new[] { "endpoint" }));
    }

    [Test]
    public void MissingRequired_is_empty_once_every_required_setting_has_a_value()
    {
        var missing = CapabilitySettingSpec.MissingRequired(
            [Endpoint, Token],
            new Dictionary<string, string> { ["endpoint"] = "http://localhost:11434" });

        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void Setting_kinds_are_a_closed_set()
    {
        Assert.That(CapabilitySettingKind.Allowed, Is.EquivalentTo(new[] { "text", "url", "secret" }));
    }
}
