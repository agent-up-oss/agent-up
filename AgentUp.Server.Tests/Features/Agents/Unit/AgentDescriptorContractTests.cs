using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentDescriptorContractTests
{
    [Test]
    public void AgentDescriptor_serializesUnavailabilityOnTheWire()
    {
        var descriptor = new AgentDescriptor(
            "claude",
            false,
            "Claude",
            true,
            "capabilityNotEnabled",
            "Claude is not enabled on this Server.",
            "Enable the claude capability.");
        var json = JsonSerializer.Serialize(descriptor, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"unavailability\":\"capabilityNotEnabled\""));
            Assert.That(json, Does.Contain("\"detail\":\"Claude is not enabled on this Server.\""));
            Assert.That(json, Does.Contain("\"remedy\":\"Enable the claude capability.\""));
            Assert.That(json, Does.Contain("\"available\":false"));
        });
    }

    [Test]
    public void AgentDescriptor_omitsACauseWhenTheAgentIsAvailable()
    {
        var descriptor = new AgentDescriptor("codex", true, "Codex");
        var json = JsonSerializer.Serialize(descriptor, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Multiple(() =>
        {
            Assert.That(descriptor.Unavailability, Is.Null);
            Assert.That(descriptor.Detail, Is.Null);
            Assert.That(descriptor.Remedy, Is.Null);
            Assert.That(json, Does.Contain("\"available\":true"));
        });
    }
}
