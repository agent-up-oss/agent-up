using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Tests.Features.Orchestration.Unit;

[TestFixture]
public sealed class WorkspaceRegistrationBuilderTests
{
    [Test]
    public void Build_IncludesDotnetAndDockerCapabilities()
    {
        var request = WorkspaceRegistrationBuilder.Build(
            new AgentUpConfiguration(
                "Typed App",
                Dotnet:
                [
                    new DotnetApplicationDefinition(
                        "Api",
                        "10.0.x",
                        new DotnetRunDefinition("src/Api/Api.csproj", ["--no-launch-profile"]),
                        [new PortDeclaration("API_PORT", 5000)])
                ],
                Docker:
                [
                    new DockerCapabilityDefinition(
                        "Database",
                        "postgres:17",
                        [new PortDeclaration("DB_PORT", 5432)])
                ]),
            new WorkspaceIdentity("/repo", "main", "abc123"),
            "/repo/worktree");

        Assert.Multiple(() =>
        {
            Assert.That(request.Dotnet, Has.Count.EqualTo(1));
            Assert.That(request.Dotnet[0].Name, Is.EqualTo("Api"));
            Assert.That(request.Docker, Has.Count.EqualTo(1));
            Assert.That(request.Docker[0].Name, Is.EqualTo("Database"));
        });
    }

    [Test]
    public void Build_includes_desktop_applications()
    {
        var request = WorkspaceRegistrationBuilder.Build(
            new AgentUpConfiguration("Desktop", DesktopApplications:
            [
                new DesktopApplicationDefinition("Editor", "dotnet run", ".", new DesktopWindowDefinition(1024, 768))
            ]),
            new WorkspaceIdentity("/repo", "main", "abc123"),
            "/repo/worktree");

        Assert.Multiple(() =>
        {
            Assert.That(request.DesktopApplications, Has.Count.EqualTo(1));
            Assert.That(request.DesktopApplications[0].Name, Is.EqualTo("Editor"));
            Assert.That(request.DesktopApplications[0].Window, Is.EqualTo(new DesktopWindowDefinition(1024, 768)));
        });
    }
}
