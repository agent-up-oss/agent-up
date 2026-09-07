using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.Providers;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Features.Database.Provider;

[TestFixture]
public class DatabaseConnectionSettingsProviderTests
{
    [Test]
    public void Resolve_BuildsPostgresSettingsFromDockerService()
    {
        var workspace = new Workspace
        {
            Id = "ws-1",
            DisplayName = "Demo",
            RepositoryPath = "/repo",
            WorktreePath = "/repo",
            Branch = "main",
            Commit = "abc",
            Applications =
            [
                new ApplicationInstance
                {
                    Name = "Database",
                    ServiceType = ServiceType.Docker,
                    Image = "postgres:16",
                    Database = true,
                    Environment = new Dictionary<string, string>
                    {
                        ["POSTGRES_USER"] = "agentup",
                        ["POSTGRES_PASSWORD"] = "secret",
                        ["POSTGRES_DB"] = "inventory"
                    },
                    AllocatedPorts =
                    [
                        new PortMapping("POSTGRES_PORT", 5432, 10602, "tcp", null, null)
                    ]
                }
            ]
        };

        var settings = new DatabaseConnectionSettingsProvider().Resolve(workspace, workspace.Applications[0]);

        Assert.That(settings.Engine, Is.EqualTo("postgres"));
        Assert.That(settings.Host, Is.EqualTo("127.0.0.1"));
        Assert.That(settings.Port, Is.EqualTo(10602));
        Assert.That(settings.Username, Is.EqualTo("agentup"));
        Assert.That(settings.Password, Is.EqualTo("secret"));
        Assert.That(settings.Database, Is.EqualTo("inventory"));
    }

    [Test]
    public void Resolve_ThrowsWhenDatabaseFlagIsDisabled()
    {
        var workspace = new Workspace
        {
            Id = "ws-1",
            DisplayName = "Demo",
            RepositoryPath = "/repo",
            WorktreePath = "/repo",
            Branch = "main",
            Commit = "abc",
            Applications =
            [
                new ApplicationInstance
                {
                    Name = "Database",
                    ServiceType = ServiceType.Docker,
                    Image = "postgres:16",
                    AllocatedPorts = [new PortMapping("POSTGRES_PORT", 5432, 10602, "tcp", null, null)]
                }
            ]
        };

        Assert.That(
            () => new DatabaseConnectionSettingsProvider().Resolve(workspace, workspace.Applications[0]),
            Throws.InvalidOperationException.With.Message.Contains("not configured as a database viewer"));
    }

    [Test]
    public void Resolve_ThrowsWhenDatabaseEngineIsUnsupported()
    {
        var workspace = new Workspace
        {
            Id = "ws-1",
            DisplayName = "Demo",
            RepositoryPath = "/repo",
            WorktreePath = "/repo",
            Branch = "main",
            Commit = "abc",
            Applications =
            [
                new ApplicationInstance
                {
                    Name = "Database",
                    ServiceType = ServiceType.Docker,
                    Image = "mysql:8",
                    Database = true,
                    AllocatedPorts = [new PortMapping("MYSQL_PORT", 3306, 10602, "tcp", null, null)]
                }
            ]
        };

        Assert.That(
            () => new DatabaseConnectionSettingsProvider().Resolve(workspace, workspace.Applications[0]),
            Throws.InvalidOperationException.With.Message.Contains("no supported database engine"));
    }

    [Test]
    public void Resolve_ThrowsWhenNoAllocatedDatabasePort()
    {
        var workspace = new Workspace
        {
            Id = "ws-1",
            DisplayName = "Demo",
            RepositoryPath = "/repo",
            WorktreePath = "/repo",
            Branch = "main",
            Commit = "abc",
            Applications =
            [
                new ApplicationInstance
                {
                    Name = "Database",
                    ServiceType = ServiceType.Docker,
                    Image = "postgres:16",
                    Database = true,
                    AllocatedPorts = []
                }
            ]
        };

        Assert.That(
            () => new DatabaseConnectionSettingsProvider().Resolve(workspace, workspace.Applications[0]),
            Throws.InvalidOperationException.With.Message.Contains("no allocated database port"));
    }
}
