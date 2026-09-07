using AgentUp.Server.Features.Database.Models;

namespace AgentUp.Server.Tests.Features.Database.Unit;

[TestFixture]
public class DatabaseConnectionSettingsTests
{
    [Test]
    public void Record_StoresConnectionValues()
    {
        var settings = new DatabaseConnectionSettings("postgres", "127.0.0.1", 10602, "agentup", "secret", "inventory");

        Assert.Multiple(() =>
        {
            Assert.That(settings.Engine, Is.EqualTo("postgres"));
            Assert.That(settings.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(settings.Port, Is.EqualTo(10602));
            Assert.That(settings.Username, Is.EqualTo("agentup"));
            Assert.That(settings.Password, Is.EqualTo("secret"));
            Assert.That(settings.Database, Is.EqualTo("inventory"));
        });
    }
}
