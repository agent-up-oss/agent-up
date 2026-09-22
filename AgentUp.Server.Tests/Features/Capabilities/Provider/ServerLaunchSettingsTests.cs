using System.Text.Json;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class ServerLaunchSettingsTests
{
    [Test]
    public void Repository_profile_uses_the_agent_up_dev_capability_registry()
    {
        var json = File.ReadAllText(LaunchSettingsPath());
        using var document = JsonDocument.Parse(json);
        var http = document.RootElement.GetProperty("profiles").GetProperty("http");
        var environment = http.GetProperty("environmentVariables");

        Assert.That(http.GetProperty("workingDirectory").GetString(), Is.EqualTo(".."));
        Assert.That(
            environment.GetProperty("AGENTUP_CAPABILITY_REGISTRY_PATH").GetString(),
            Is.EqualTo(".agent-up-dev/capability-registry"));
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
