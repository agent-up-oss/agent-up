using AgentUp.Server.Features.Verification.Controllers;
using AgentUp.Server.Features.Verification.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Server.Tests.Features.Verification.Controller;

[TestFixture]
public sealed class VerificationControllerTests
{
    [Test]
    public async Task RunAndGuardAsync_delegatesToTheQueueGate()
    {
        var plans = new VerificationPlanService(
            new StubVerificationConfigurationLoader(VerificationConfiguration.Empty),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider("linux")),
            [new StaticChangedContentSource(new Dictionary<string, string> { ["a.cs"] = "sha256:a" })]);
        var ledger = new InMemoryReceiptLedgerStore();
        var controller = new VerificationController(
            new VerificationQueueGateService(
                plans,
                new VerificationRunService(plans, ledger, new ScriptedCheckRunner(), new FakeVerificationClock()),
                new VerificationGuardService(plans, ledger)));

        var result = await controller.RunAndGuardAsync("/repo");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("requires a configured verification section"));
    }
}
