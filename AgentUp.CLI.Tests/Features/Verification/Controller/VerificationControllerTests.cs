using AgentUp.CLI.Features.Verification.Controllers;
using AgentUp.CLI.Features.Verification.Services;
using AgentUp.CLI.Tests.Fake;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;

namespace AgentUp.CLI.Tests.Features.Verification.Controller;

[TestFixture]
public sealed class VerificationControllerTests
{
    private const string Worktree = "/repo";
    private const string CliSource = "AgentUp.CLI/Features/Verification/Controllers/VerifyRunCommand.cs";

    private static VerificationConfiguration Rules(VerificationEnforcement enforcement = VerificationEnforcement.Block)
        => new(
            enforcement,
            [],
            new Dictionary<string, CheckDefinition>(StringComparer.Ordinal)
            {
                ["cli"] = new("cli", "dotnet test AgentUp.CLI.Tests", null, CheckTier.Fast, [], false, ["AgentUp.CLI"])
            },
            [new VerificationPathRule("AgentUp.CLI/**", ["cli"])]);

    private static IReadOnlyDictionary<string, string> Changed(params string[] paths)
        => paths.ToDictionary(path => path, path => "sha256:" + path.Length, StringComparer.Ordinal);

    /// <summary>
    /// Assembles the controller over an explicit world. Each test declares its own inputs
    /// here rather than sharing a mutable fixture, so the arrangement reads inline.
    /// </summary>
    private static VerificationController ControllerOver(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changed,
        IReceiptLedgerStore ledger,
        ScriptedCheckRunner runner,
        TextWriter output,
        TextWriter error,
        IVerificationConfigurationLoader? loader = null)
    {
        var plans = new VerificationPlanService(
            loader ?? new StubVerificationConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider()),
            [new StaticChangedContentSource(changed)]);
        var runs = new VerificationRunService(plans, ledger, runner, new FakeVerificationClock());
        var guards = new VerificationGuardService(plans, ledger);
        var outputService = new VerifyOutputService(output, error);
        var commands = new VerifyCommandService(plans, runs, guards);

        return new VerificationController(
            new VerifyPlanCommand(commands, outputService),
            new VerifyRunCommand(commands, outputService),
            new VerifyGuardCommand(commands, outputService),
            outputService,
            Worktree);
    }

    [Test]
    public async Task RunAsync_planListsTheRequiredChecks()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var controller = ControllerOver(Rules(), Changed(CliSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner(), output, error);

        var code = await controller.RunAsync(["plan"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("cli"));
        });
    }

    [Test]
    public async Task RunAsync_guardReturnsTwoWhenARequiredCheckHasNeverRun()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var controller = ControllerOver(Rules(), Changed(CliSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner(), output, error);

        Assert.That(await controller.RunAsync(["guard"]), Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_runThenGuardSatisfiesTheGate()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var controller = ControllerOver(Rules(), Changed(CliSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner(), output, error);

        await controller.RunAsync(["run"]);

        Assert.That(await controller.RunAsync(["guard"]), Is.Zero);
    }

    [Test]
    public async Task RunAsync_guardWithRunExecutesWhatIsMissingThenPasses()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new ScriptedCheckRunner();
        var controller = ControllerOver(Rules(), Changed(CliSource), new InMemoryReceiptLedgerStore(), runner, output, error);

        var code = await controller.RunAsync(["guard", "--run"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(runner.Executed, Is.EqualTo(new[] { "cli" }));
        });
    }

    [Test]
    public async Task RunAsync_guardWithoutRunExecutesNothing()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new ScriptedCheckRunner();
        var controller = ControllerOver(Rules(), Changed(CliSource), new InMemoryReceiptLedgerStore(), runner, output, error);

        await controller.RunAsync(["guard"]);

        Assert.That(runner.Executed, Is.Empty,
            "The default guard must stay cheap enough for a hook that fires every turn.");
    }

    [Test]
    public async Task RunAsync_guardAcceptsTheHookFormatAsTwoArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var controller = ControllerOver(Rules(), Changed(CliSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner(), output, error);

        var code = await controller.RunAsync(["guard", "--format", "hook"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(2));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("[agent-up]"));
        });
    }

    [Test]
    public async Task RunAsync_runAcceptsASingleCheckId()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new ScriptedCheckRunner();
        var controller = ControllerOver(Rules(), Changed(CliSource), new InMemoryReceiptLedgerStore(), runner, output, error);

        await controller.RunAsync(["run", "cli"]);

        Assert.That(runner.Executed, Is.EqualTo(new[] { "cli" }));
    }

    [Test]
    public async Task RunAsync_unknownSubcommandWritesUsage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var controller = ControllerOver(Rules(), Changed(CliSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner(), output, error);

        var code = await controller.RunAsync(["wat"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("Usage: agentup verify"));
        });
    }

    [Test]
    public async Task RunAsync_reportsAConfigurationErrorInsteadOfPassingVacuously()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var controller = ControllerOver(
            Rules(),
            Changed(CliSource),
            new InMemoryReceiptLedgerStore(),
            new ScriptedCheckRunner(),
            output,
            error,
            new ThrowingVerificationConfigurationLoader("agent-up.json is not valid JSON"));

        var code = await controller.RunAsync(["guard"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("not valid JSON"));
        });
    }
}
