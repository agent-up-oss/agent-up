using System.Text.Json;
using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Providers;
using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Services;

namespace AgentUp.Capabilities.Opencode.Tests.Features.OpencodeCapability.Unit;

[TestFixture]
public sealed class OpencodeAgentCapabilityTests
{
    [Test]
    public void Launch_starts_opencode_serving_acp_over_stdio()
    {
        var launch = new OpencodeAgentCapability().Launch(Configured());

        Assert.That(launch.FileName, Is.EqualTo("opencode"));
        Assert.That(launch.Arguments, Is.EqualTo(new[] { "acp" }));
        Assert.That(launch.NixPackage, Is.EqualTo("opencode"));
    }

    [Test]
    public void Launch_points_opencode_at_the_configured_endpoint_and_model()
    {
        var config = Config(new OpencodeAgentCapability().Launch(Configured()).Environment!);

        Assert.That(config.GetProperty("model").GetString(), Is.EqualTo("agent-up/qwen2.5-coder:7b"));
        Assert.That(Provider(config).GetProperty("options").GetProperty("baseURL").GetString(), Is.EqualTo("http://localhost:11434/v1"));
    }

    [Test]
    public void Launch_passes_the_token_by_variable_and_keeps_it_out_of_the_configuration()
    {
        var environment = new OpencodeAgentCapability().Launch(Configured(token: "sk-secret")).Environment!;

        Assert.That(environment[OpencodeConfigProvider.TokenVariable], Is.EqualTo("sk-secret"));
        Assert.That(environment[OpencodeConfigProvider.ConfigVariable], Does.Not.Contain("sk-secret"));
        Assert.That(environment[OpencodeConfigProvider.ConfigVariable], Does.Contain("{env:AGENT_UP_OPENCODE_API_KEY}"));
    }

    [Test]
    public void Launch_without_a_token_sends_no_api_key()
    {
        var environment = new OpencodeAgentCapability().Launch(Configured()).Environment!;

        Assert.That(environment.ContainsKey(OpencodeConfigProvider.TokenVariable), Is.False);
        Assert.That(Provider(Config(environment)).GetProperty("options").TryGetProperty("apiKey", out _), Is.False);
    }

    [Test]
    public void Launch_asks_before_edits_and_commands_so_a_permission_policy_governs_them()
    {
        var permission = Config(new OpencodeAgentCapability().Launch(Configured()).Environment!).GetProperty("permission");

        Assert.That(permission.GetProperty("edit").GetString(), Is.EqualTo("ask"));
        Assert.That(permission.GetProperty("bash").GetString(), Is.EqualTo("ask"));
    }

    [Test]
    public void Endpoint_and_model_are_required_and_the_token_is_a_secret()
    {
        var settings = new OpencodeAgentCapability().Settings;

        Assert.That(settings.Where(spec => spec.Required).Select(spec => spec.Key), Is.EquivalentTo(new[] { "endpoint", "model" }));
        Assert.That(settings.Single(spec => spec.Key == "token").Kind, Is.EqualTo("secret"));
    }

    [Test]
    public void Opencode_needs_no_sign_in_and_asks_for_permission()
    {
        var agent = new OpencodeAgentCapability();

        Assert.That(agent.Login, Is.Null);
        Assert.That(agent.SupportsPermissionRequests, Is.True);
        Assert.That(agent.Software.Single().DeliveredByNix, Is.True);
    }

    private static Dictionary<string, string> Configured(string? token = null)
    {
        var settings = new Dictionary<string, string>
        {
            [OpencodeAgentCapability.EndpointKey] = "http://localhost:11434",
            [OpencodeAgentCapability.ModelKey] = "qwen2.5-coder:7b"
        };
        if (token is not null)
            settings[OpencodeAgentCapability.TokenKey] = token;
        return settings;
    }

    private static JsonElement Config(IReadOnlyDictionary<string, string> environment)
        => JsonDocument.Parse(environment[OpencodeConfigProvider.ConfigVariable]).RootElement;

    private static JsonElement Provider(JsonElement config)
        => config.GetProperty("provider").GetProperty(OpencodeConfigProvider.ProviderId);
}
