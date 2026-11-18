using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilitySettingsStoreTests
{
    [Test]
    public void Write_then_Read_round_trips_one_capability_without_touching_another()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-settings");
        try
        {
            var store = new CapabilitySettingsStore(root.FullName);
            store.Write("opencode", new Dictionary<string, string> { ["endpoint"] = "http://a.test" });
            store.Write("other", new Dictionary<string, string> { ["key"] = "value" });

            var reread = new CapabilitySettingsStore(root.FullName);

            Assert.That(reread.Read("opencode")["endpoint"], Is.EqualTo("http://a.test"));
            Assert.That(reread.Read("other")["key"], Is.EqualTo("value"));
            Assert.That(reread.Read("missing"), Is.Empty);
        }
        finally { root.Delete(true); }
    }

    [Test]
    public void The_settings_file_is_readable_by_the_server_user_only()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-settings-mode");
        try
        {
            new CapabilitySettingsStore(root.FullName).Write("opencode", new Dictionary<string, string> { ["token"] = "sk" });
            var path = Path.Join(root.FullName, "capabilities", "settings.json");

            Assert.That(
                OperatingSystem.IsWindows() || File.GetUnixFileMode(path) == (UnixFileMode.UserRead | UnixFileMode.UserWrite),
                Is.True);
        }
        finally { root.Delete(true); }
    }
}
