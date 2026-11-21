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
    public void WriteGrant_keepsTheRefreshTokenReadableOnlyByTheServer()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"forge-creds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new ForgeCredentialStore(root);
            var expires = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
            store.WriteGrant("Primary", "forgejo", new AgentUp.Server.Features.Forge.Models.ForgeOAuthGrant("access", "refresh", expires));
            var grant = store.ReadGrant("Primary", "forgejo");
            store.Write("Primary", "forgejo", "replaced");

            Assert.Multiple(() =>
            {
                Assert.That(grant, Is.EqualTo(new AgentUp.Server.Features.Forge.Models.ForgeOAuthGrant("access", "refresh", expires)));
                Assert.That(File.GetUnixFileMode(Path.Join(root, "forge-credentials", "Primary", "forgejo")),
                    Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
                Assert.That(store.ReadGrant("Primary", "forgejo")!.RefreshToken, Is.Null, "a token written alone drops the old refresh token");
            });
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
            Assert.That(() => store.Write("../escape", "github", "token"), Throws.InvalidOperationException);
            Assert.That(() => store.Write("acme/widgets", "github", "token"), Throws.InvalidOperationException);
            Assert.That(() => store.Write("ok", "git\\hub", "token"), Throws.InvalidOperationException);
            Assert.That(() => store.Write(".", "github", "token"), Throws.InvalidOperationException);
            Assert.That(() => store.Write(" ", "github", "token"), Throws.InvalidOperationException);
            Assert.That(Directory.Exists(Path.Join(root, "escape")), Is.False);
            Assert.That(Directory.Exists(Path.Join(root, "forge-credentials")), Is.False);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void IsUnderRoot_rejectsAPathOutsideTheStore()
    {
        var root = Path.Join(Path.GetTempPath(), "forge-root");
        Assert.That(ForgeCredentialStore.IsUnderRoot(root, Path.Join(root, "github")), Is.True);
        Assert.That(ForgeCredentialStore.IsUnderRoot(root, Path.GetFullPath(Path.Join(root, "..", "outside"))), Is.False);
    }
}
