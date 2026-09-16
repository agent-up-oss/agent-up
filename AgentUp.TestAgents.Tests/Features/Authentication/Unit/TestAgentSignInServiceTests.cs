using AgentUp.TestAgents.Features.Authentication.Services;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class TestAgentSignInServiceTests
{
    // Which flow a schema maps to is the whole decision this service makes. The client id each
    // flow presents is how the identity provider tells the agents apart, so a schema wired to the
    // wrong flow would sign the wrong agent in and still look like it worked.
    [Test]
    public void Flow_mapsEachSchemaToTheAgentThatImplementsIt()
    {
        using var client = new HttpClient();
        var service = new TestAgentSignInService();

        Assert.Multiple(() =>
        {
            Assert.That(service.Flow(TestAgentSchema.LoopbackRedirect, client, "http://idp").ClientId, Is.EqualTo("test-agent1"));
            Assert.That(service.Flow(TestAgentSchema.DeviceCode, client, "http://idp").ClientId, Is.EqualTo("test-agent2"));
            Assert.That(service.Flow(TestAgentSchema.PastedCode, client, "http://idp").ClientId, Is.EqualTo("test-agent3"));
            Assert.That(service.Flow(TestAgentSchema.SilentPoll, client, "http://idp").ClientId, Is.EqualTo("test-agent4"));
        });
    }

    [Test]
    public void Flow_refusesASchemaNoAgentImplements()
    {
        using var client = new HttpClient();
        var service = new TestAgentSignInService();

        Assert.That(
            () => service.Flow((TestAgentSchema)999, client, "http://idp"),
            Throws.InvalidOperationException.With.Message.Contains("No sign-in is defined"));
    }

    [Test]
    public void Credentials_handsOutAStorePerAgentRatherThanOneSharedAcrossThem()
    {
        var service = new TestAgentSignInService();

        Assert.That(
            service.Credentials(TestAgentSchema.DeviceCode),
            Is.Not.SameAs(service.Credentials(TestAgentSchema.PastedCode)),
            "Signing one agent in must not make another look signed in");
    }
}
