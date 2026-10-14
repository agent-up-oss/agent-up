using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Shared.Providers;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class ExportCannotSatisfyGuardTests
{
    private const string Root = "/repo";

    [Test]
    public async Task GuardAsync_isUnchangedWhenAnExportDocumentIsPresentedAsTheLedger()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();
        var plans = new VerificationPlanService(
            new StubConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(VerificationDomain.Linux)),
            [new StaticChangedContentSource("test", changed)]);
        var empty = new InMemoryReceiptLedgerStore();
        var exportShaped = new InMemoryReceiptLedgerStore(new ReceiptLedger([
            new VerificationReceipt(
                VerificationDomain.ServerUnitCheck,
                "dotnet test AgentUp.Server.Tests",
                0,
                "2026-09-11T12:00:00.0000000+00:00",
                10,
                new Dictionary<string, string>(StringComparer.Ordinal))
        ]));

        var emptyReport = await new VerificationGuardService(plans, empty).GuardAsync(Root);
        var exportReport = await new VerificationGuardService(plans, exportShaped).GuardAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(emptyReport.Satisfied, Is.False);
            Assert.That(exportReport.Satisfied, Is.False);
            Assert.That(
                exportReport.Verdicts.Single(verdict => verdict.CheckId == VerificationDomain.ServerUnitCheck).Kind,
                Is.EqualTo(CheckVerdictKind.Stale));
        });
    }

    [Test]
    public void GuardService_doesNotAcceptAnExportType()
    {
        var parameters = typeof(VerificationGuardService)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        Assert.That(parameters, Does.Not.Contain(typeof(VerificationExport)));
        Assert.That(parameters, Does.Not.Contain(typeof(VerificationExportService)));
    }
}
