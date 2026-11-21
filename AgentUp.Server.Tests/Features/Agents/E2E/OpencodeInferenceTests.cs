using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Services;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Features.Agents.Unit;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

/// <summary>
/// The real OpenCode binary, launched by the built-in OpenCode capability with the endpoint and
/// model a user would set in Settings, against the pinned in-process model served by
/// <c>test-model-server</c>. One unattended author iteration under a permission policy has to end
/// with the file OpenCode was asked to write, the policy's rule recorded, and every loop invariant
/// held. <c>.github/scripts/run-opencode-inference.sh</c> provides the binary and the endpoint.
/// </summary>
[TestFixture]
[Category("OpencodeInference")]
[Timeout(900_000)]
[CancelAfter(900_000)]
public sealed class OpencodeInferenceTests
{
    private const string Content = "written by opencode";

    [Test]
    public async Task OpenCode_writes_the_requested_file_under_a_permission_policy()
    {
        var opencode = Required("AGENTUP_OPENCODE_INFERENCE_BINARY");
        var endpoint = Required("AGENTUP_OPENCODE_INFERENCE_ENDPOINT");
        var apiKey = Required("AGENTUP_OPENCODE_INFERENCE_API_KEY");
        var worktree = Directory.CreateTempSubdirectory("agent-up-opencode-inference").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Join(worktree, "README.md"), "opencode inference worktree\n");
            var result = await RunAsync(opencode, endpoint, apiKey, worktree,
                $"Create a file named result.txt containing exactly: {Content}");
            TestContext.Out.WriteLine($"opencode-inference terminal={result.Terminal} stop={result.StopReason} rules={string.Join(",", result.PermissionRuleIds)}");

            var written = Path.Join(worktree, "result.txt");
            Assert.That(File.Exists(written), Is.True, "OpenCode did not write result.txt.");
            Assert.That((await File.ReadAllTextAsync(written)).Trim(), Is.EqualTo(Content));
            Assert.That(result.PermissionRuleIds, Does.Contain("opencode-allow-once"));
            Assert.That(new LoopInvariantGuard().Evaluate(result), Is.Empty);
        }
        finally
        {
            try { Directory.Delete(worktree, recursive: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                TestContext.Out.WriteLine($"Could not remove {worktree}: {exception.Message}");
            }
        }
    }

    private static async Task<LoopRunResult> RunAsync(string opencode, string endpoint, string apiKey, string worktree, string prompt)
    {
        // The capability exactly as the Server holds it, enabled with the settings a user saves.
        // Nix is reported absent so the launch is the binary the script installed, not a Nix shell.
        var modules = CapabilityModuleHarness.CreateService(nixAvailable: false, enabled: true, modules: [new OpencodeAgentCapability()]);
        modules.UpdateSettings("opencode", new Dictionary<string, string?>
        {
            [OpencodeAgentCapability.EndpointKey] = endpoint,
            [OpencodeAgentCapability.TokenKey] = apiKey,
            [OpencodeAgentCapability.ModelKey] = "qwen2.5-1.5b-instruct"
        });
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService(modules)),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(
            ServerDomain.Workspace().Named("OpencodeInference").At(worktree).AtCommit("abc").Build());
        var commands = new AgentCommandProvider(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:opencode:Command"] = opencode
            }).Build(),
            modules);
        await using var scheduling = new AgentSchedulingService(
            new WorkspaceQueryController(registry),
            new AgentProcessFactory(commands, NullLoggerFactory.Instance),
            commands,
            new FakeSubscriptionLoginProvider(),
            new NoExtraEnvironment(),
            new FakeClaudeCredentialStore(),
            new AgentSubscriptionAuth(),
            new AgentEventFrameProvider(),
            new AgentEventService(new AgentEventFrameProvider()),
            NullLogger<AgentSchedulingService>.Instance,
            modules);
        var scheduled = await scheduling.ScheduleAsync(workspace.Id, "opencode", CancellationToken.None);
        if (scheduled.Error is not null)
            throw new InvalidOperationException(scheduled.Error);
        var loop = new AutonomousLoopService(scheduling, new LoopInvariantGuard(), new ImmediateLoopBackoff());
        return await loop.RunAsync(
            new LoopRunRequest(
                workspace.Id,
                prompt,
                1,
                new AllowListedPermissionPolicy("once", "opencode-allow-once"),
                100_000,
                42,
                // A cold turn loads the GGUF, lets OpenCode install its OpenAI-compatible provider,
                // and evaluates the prompt on CPU; the kernel's 30-second default measures none of that.
                PromptTimeoutMs: 300_000),
            CancellationToken.None);
    }

    private static string Required(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is not set; run .github/scripts/run-opencode-inference.sh.");

    private sealed class NoExtraEnvironment : IAgentProcessEnvironmentProvider
    {
        public IReadOnlyDictionary<string, string> EnvironmentFor(string agent) => new Dictionary<string, string>();
    }
}
