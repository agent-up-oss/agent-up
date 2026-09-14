using AgentUp.TestAgents.Features.Authentication.Providers;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class TestAgentCredentialStoreTests
{
    private string _home = null!;

    [SetUp]
    public void CreateHome()
    {
        // The store keeps credentials under HOME the way the real agent CLIs do, which is how the
        // Server keeps them inside its own data directory rather than the service account's home.
        _home = Directory.CreateTempSubdirectory("agent-up-test-agent-home").FullName;
    }

    [TearDown]
    public void RemoveHome()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp directory is not worth failing a test over.
            TestContext.WriteLine($"Could not remove {_home}: {exception.Message}");
        }
    }

    [Test]
    public void Read_returnsNothingBeforeTheAgentHasSignedIn()
    {
        Assert.That(Store(TestAgentSchema.DeviceCode).Read(), Is.Null);
    }

    [Test]
    public void Write_thenRead_roundTripsTheToken()
    {
        Store(TestAgentSchema.DeviceCode).Write("test-oat-abc123");

        Assert.That(Store(TestAgentSchema.DeviceCode).Read(), Is.EqualTo("test-oat-abc123"));
    }

    // Each agent keeps its own credential, so signing one in must not make another look signed in.
    // Getting this wrong would let a test pass without the sign-in under test having happened.
    [Test]
    public void Write_keepsEachAgentsCredentialSeparate()
    {
        Store(TestAgentSchema.DeviceCode).Write("test-oat-device");

        Assert.Multiple(() =>
        {
            Assert.That(Store(TestAgentSchema.DeviceCode).Read(), Is.EqualTo("test-oat-device"));
            Assert.That(Store(TestAgentSchema.PastedCode).Read(), Is.Null);
            Assert.That(Store(TestAgentSchema.SilentPoll).Read(), Is.Null);
            Assert.That(Store(TestAgentSchema.LoopbackRedirect).Read(), Is.Null);
        });
    }

    [Test]
    public void Read_treatsABlankFileAsNotSignedIn()
    {
        Store(TestAgentSchema.LoopbackRedirect).Write("   ");

        Assert.That(Store(TestAgentSchema.LoopbackRedirect).Read(), Is.Null);
    }

    private TestAgentCredentialStore Store(TestAgentSchema schema) => new(schema, _home);
}
