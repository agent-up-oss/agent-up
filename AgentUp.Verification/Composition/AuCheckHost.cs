using System.Reflection;
using AgentUp.Verification.Features.Coverage.Controllers;
using AgentUp.Verification.Features.Coverage.Providers;
using AgentUp.Verification.Features.Coverage.Services;
using AgentUp.Verification.Features.Verification.Controllers;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Composition;

/// <summary>
/// Wires <c>au-check</c> onto the verification library. The executable does not resolve checks;
/// <see cref="CheckPlanProvider"/> remains the only resolver.
/// </summary>
public static class AuCheckHost
{
    public static Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var invocation = new AuCheckArgumentParser().Parse(args, Directory.GetCurrentDirectory());
        if (invocation.Error is not null)
        {
            Console.Error.WriteLine(invocation.Error);
            return Task.FromResult(AuCheckExit.Invalid);
        }

        if (invocation.Command == "help")
        {
            Console.Out.WriteLine(AuCheckHelp.Text);
            return Task.FromResult(0);
        }

        var configPath = Path.IsPathRooted(invocation.ConfigPath)
            ? invocation.ConfigPath
            : Path.Join(invocation.RepositoryRoot, invocation.ConfigPath);
        invocation = invocation with { ConfigPath = configPath };

        var globs = new PathGlobProvider();
        var ledger = new FileReceiptLedgerStore(new GitDirectoryProvider());
        var platform = new PlatformCapabilityProvider();
        var planService = new VerificationPlanService(
            new VerificationConfigurationLoader(configPath, invocation.ConfigExplicit),
            new CheckPlanProvider(globs, platform),
            [new GitChangedContentSource(new ContentHashProvider(), new GitChangeOutputParser())]);
        var runService = new VerificationRunService(
            planService, ledger, new ProcessCheckRunner(), new VerificationClock());
        var guardService = new VerificationGuardService(planService, ledger);
        var coverage = new PatchCoverageController(new PatchCoverageService(
            new CoverageConfigurationLoader(configPath, invocation.ConfigExplicit),
            new CoverageReportReader(new CoberturaReportParser()),
            [new GitChangedLineSource(new UnifiedDiffParser())],
            globs));
        var sliceCoverage = new SliceCoverageController(new SliceCoverageService(
            new CoverageConfigurationLoader(configPath, invocation.ConfigExplicit),
            new CoverageReportReader(new CoberturaReportParser()),
            globs));
        var commands = new AuCheckCommandService(
            planService,
            runService,
            guardService,
            coverage,
            sliceCoverage,
            new GitRevisionProvider(new GitDirectoryProvider()),
            new ContentHashProvider(),
            new RunReportWriter(new ChromiumReportPageCapture()),
            ToolVersion(),
            Console.Out,
            Console.Error);
        return new AuCheckController(commands).RunAsync(invocation, cancellationToken);
    }

    private static string ToolVersion()
        => typeof(AuCheckHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
           ?? typeof(AuCheckHost).Assembly.GetName().Version?.ToString()
           ?? "0.0.0";
}
