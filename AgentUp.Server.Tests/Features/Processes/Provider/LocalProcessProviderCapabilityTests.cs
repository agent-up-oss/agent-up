using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Providers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public sealed class LocalProcessProviderCapabilityTests
{
    [Test]
    public void CreateStartInfo_uses_hosted_launch_spec_without_reparsing_the_command()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(worktreePath);
        try
        {
            var app = new ApplicationInstance
            {
                Name = "api",
                Command = "should-not-parse",
                LaunchFileName = "dotnet",
                LaunchArguments = ["run", "--project", "Api.csproj"],
                CapabilityId = "dotnet"
            };
            var workspace = new Workspace
            {
                Id = "ws",
                DisplayName = "ws",
                RepositoryPath = worktreePath,
                WorktreePath = worktreePath,
                Branch = "main",
                Commit = "abc",
                Applications = [app]
            };

            var startInfo = new LocalProcessProvider().CreateStartInfo(workspace, app);

            Assert.That(startInfo.FileName, Is.EqualTo("dotnet"));
            Assert.That(startInfo.ArgumentList[0], Is.EqualTo("run"));
            Assert.That(startInfo.ArgumentList[1], Is.EqualTo("--project"));
            Assert.That(startInfo.ArgumentList[2], Does.EndWith("Api.csproj"));
        }
        finally
        {
            Directory.Delete(worktreePath, recursive: true);
        }
    }
}
