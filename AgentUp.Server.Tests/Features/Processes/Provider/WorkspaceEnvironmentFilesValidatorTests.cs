using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Providers;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public sealed class WorkspaceEnvironmentFilesValidatorTests
{
    [Test]
    public void Validate_AllowsNestedEnvironmentFileUnderWorkspaceRoot()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Join(root, "Examples", "full-stack-react"));
        File.WriteAllText(Path.Join(root, "Examples", "full-stack-react", "database.env"), "POSTGRES_DB=agentup");

        try
        {
            WorkspaceEnvironmentFilesValidator.Validate(
                root,
                [new ApplicationEnvironmentFileSource(
                    "Database",
                    ["Examples/full-stack-react/database.env"])]);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Validate_RejectsEnvironmentFileOutsideWorkspaceRoot()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkspaceEnvironmentFilesValidator.Validate(
                "/repo",
                [new ApplicationEnvironmentFileSource(
                    "Database",
                    ["../.env"])]));

        Assert.That(ex!.Message, Does.Contain("must stay under the workspace root"));
    }
}
