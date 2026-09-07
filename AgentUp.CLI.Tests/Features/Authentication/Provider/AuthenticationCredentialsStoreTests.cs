using AgentUp.CLI.Features.Authentication.Providers;

namespace AgentUp.CLI.Tests.Features.Authentication.Provider;

[TestFixture]
public class AuthenticationCredentialsStoreTests
{
    [Test]
    public void SetToken_persistsAndNormalizesServerUrl()
    {
        var directory = CreateTempDirectory();
        var store = new AuthenticationCredentialsStore(directory);

        store.SetToken("http://localhost:5000/", "token-1");

        Assert.That(store.GetToken("http://localhost:5000"), Is.EqualTo("token-1"));
    }

    [Test]
    public void ClearToken_removesStoredToken()
    {
        var directory = CreateTempDirectory();
        var store = new AuthenticationCredentialsStore(directory);
        store.SetToken("http://localhost:5000", "token-1");

        store.ClearToken("http://localhost:5000");

        Assert.That(store.GetToken("http://localhost:5000"), Is.Null);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Join(Path.GetTempPath(), "AgentUp-CLI-Auth", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
