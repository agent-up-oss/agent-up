using System.Text.Json;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class ServerLaunchSettingsTests
{
    [Test]
    public void Repository_profile_seeds_the_enabled_set_from_agent_up_dev_and_names_no_registry()
    {
        var json = File.ReadAllText(LaunchSettingsPath());
        using var document = JsonDocument.Parse(json);
        var http = document.RootElement.GetProperty("profiles").GetProperty("http");
        var environment = http.GetProperty("environmentVariables");

        Assert.That(http.GetProperty("workingDirectory").GetString(), Is.EqualTo(".."));
        Assert.That(environment.TryGetProperty("AGENTUP_CAPABILITY_REGISTRY_PATH", out _), Is.False);
        Assert.That(
            environment.GetProperty("AGENTUP_CAPABILITY_ENABLED_PATH").GetString(),
            Is.EqualTo(".agent-up-dev/enabled.json"));
    }

    private static string LaunchSettingsPath()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Join(directory, "AgentUp.Server", "Properties", "launchSettings.json");
            if (File.Exists(candidate))
                return candidate;

            var parent = Path.GetDirectoryName(directory);
            if (parent == directory)
                break;
            directory = parent;
        }

        throw new FileNotFoundException("AgentUp.Server launchSettings.json was not found.");
    }
}
