using AgentUp.Server.Features.Forge.Providers;

namespace AgentUp.Server.Tests.Features.Forge.Provider;

[TestFixture]
public sealed class ForgeCredentialStoreTests
{
    [Test]
    public void Write_then_Read_roundTripsATokenUnderTheWorkspace()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"forge-creds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new ForgeCredentialStore(root);
            store.Write("Primary", "github", "ghp_token");

            Assert.That(store.Read("Primary", "github"), Is.EqualTo("ghp_token"));
            Assert.That(File.Exists(Path.Join(root, "forge-credentials", "Primary", "github")), Is.True);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Delete_removesTheStoredToken()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"forge-creds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new ForgeCredentialStore(root);
            store.Write("Primary", "github", "ghp_token");
            store.Delete("Primary", "github");

            Assert.That(store.Read("Primary", "github"), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void PathFor_rejectsAWorkspaceIdThatEscapesTheStore()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"forge-creds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new ForgeCredentialStore(root);
            Assert.That(() => store.Write("../escape", "github", "token"), Throws.Nothing);
            Assert.That(Directory.Exists(Path.Join(root, "escape")), Is.False);
            Assert.That(
                File.Exists(Path.Join(root, "forge-credentials", Uri.EscapeDataString("../escape"), "github")),
                Is.True);
            Assert.That(store.Read("../escape", "github"), Is.EqualTo("token"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
