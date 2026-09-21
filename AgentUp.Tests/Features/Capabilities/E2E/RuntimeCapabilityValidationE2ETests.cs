using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.Services;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Tests.Support;
using Avalonia.Threading;

namespace AgentUp.Tests.Features.Capabilities.E2E;

[TestFixture, Category("RuntimeCapabilityE2E"), NonParallelizable]
[Platform(Include = "Linux")]
[Timeout(1500000), CancelAfter(1500000)]
public sealed class RuntimeCapabilityValidationE2ETests
{
    private const string ExampleWeb = "Example Web";
    private const string ExampleApi = "Example API";
    private const string FlowId = "b6e1c8a04f5d4e2f9a7c3d1e8b0f2468";
    private static readonly string[] WorkspaceApplications =
    [
        "Docs",
        "Example Web",
        "Example API",
        "Database",
        "Sample Desktop"
    ];
    private static readonly TimeSpan WorkspaceHttpTimeout = TimeSpan.FromMinutes(20);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static DesktopStreamingServer? Server;
    private static DesktopBrowserHarness? Desktop;
    private static string? WorkspaceId;
    private static string? RegistryRoot;
    private static Exception? StartFailure;

    [OneTimeTearDown]
    public async Task StopRuntimeWorkspace()
    {
        if (Desktop is not null)
            await Desktop.DisposeAsync();
        if (Server is not null)
        {
            try
            {
                if (WorkspaceId is not null)
                    await Server.Client.PostAsync($"/api/workspaces/{WorkspaceId}/stop", null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                TestContext.Progress.WriteLine(ex.Message);
            }

            await Server.DisposeAsync();
        }

        if (RegistryRoot is not null)
        {
            try
            {
                Directory.Delete(RegistryRoot, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TestContext.Progress.WriteLine(ex.Message);
            }
        }
    }

    [Test]
    public async Task Desktop_start_replays_example_web_validation_flow()
    {
        var workspaceId = await EnsureRuntimeWorkspaceAsync();
        var (passed, message) = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var viewModel = Desktop!.ViewModel;
            await viewModel.Validation!.LoadAsync(workspaceId, ExampleWeb);
            var item = viewModel.Validation.Flows.SingleOrDefault(flow => flow.Flow.Id == FlowId);
            if (item is null)
                return (false, viewModel.Validation.Status ?? "Desktop Validation did not load the recorded Example Web flow.");

            if (viewModel.ValidationReplay is not ValidationFlowReplayService replay)
                return (false, "Desktop Validation replay is not connected to the browser view.");

            await replay.RunAsync(workspaceId, item.Flow.Id, item);
            return (item.RunState == ValidationRunState.Passed, item.ResultMessage);
        });

        Assert.That(passed, Is.True, message ?? "Example Web validation replay failed.");
    }

    [Test]
    public async Task Capability_hosted_api_serves_seeded_postgres_rows()
    {
        var workspaceId = await EnsureRuntimeWorkspaceAsync();
        var port = await WaitForAllocatedHttpPortAsync(workspaceId, ExampleApi);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        using var response = await WaitForHealthyAsync(client);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.That(body.GetProperty("ok").GetBoolean(), Is.True);
        Assert.That(body.GetProperty("product_count").GetInt32(), Is.GreaterThan(0));
    }

    private static async Task<string> EnsureRuntimeWorkspaceAsync()
    {
        if (WorkspaceId is not null)
            return WorkspaceId;
        if (StartFailure is not null)
            throw StartFailure;

        try
        {
            WorkspaceId = await StartRuntimeWorkspaceAsync();
            return WorkspaceId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not IgnoreException)
        {
            StartFailure = ex;
            throw;
        }
    }

    private static async Task<string> StartRuntimeWorkspaceAsync()
    {
        var repoRoot = FindRepositoryRoot();
        if (string.IsNullOrWhiteSpace(repoRoot))
            Assert.Fail("Could not find the Agent-Up repository root from the test directory.");
        var root = repoRoot!;

        Assert.That(File.Exists(Path.Join(root, "agent-up.json")), Is.True);

        var registry = PackRuntimeCapabilities(root);
        var enabledPath = Path.Join(registry, "enabled.json");
        await File.WriteAllTextAsync(enabledPath, """{"schemaVersion":"1","modules":[]}""");
        Stage($"Capability registry ready at {registry}.");

        var started = Stopwatch.StartNew();
        Server = await DesktopStreamingServer.StartAsync(registry, enabledPath);
        Server.Client.Timeout = WorkspaceHttpTimeout;
        Stage($"Runtime capability Server listening at {Server.BaseUri}.");

        Desktop = await DesktopBrowserHarness.LaunchAgainstServerAsync(
            Server.BaseUri,
            waitForWebView: false,
            httpTimeout: WorkspaceHttpTimeout);
        Stage("Desktop mounted against the Server.");
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var modules = Desktop.ViewModel.Modules
                ?? throw new InvalidOperationException("Desktop did not mount the capability modules catalog.");
            await modules.OpenAsync();
            await modules.EnableAsync("dotnet", "1.0.0");
            await modules.EnableAsync("docker", "1.0.0");
            AssertEnabledOnUi("dotnet");
            AssertEnabledOnUi("docker");
            modules.Close();
        });
        Stage("Desktop enabled the dotnet and docker capability modules.");

        var request = await Server.GetRequiredService<OrchestrationRegistrationService>().BuildAsync(root, CancellationToken.None);
        Assert.That(request, Is.Not.Null);
        Assert.That(
            request!.RuntimeSections.Select(section => section.ModuleId).ToArray(),
            Is.EquivalentTo(new[] { "dotnet", "docker" }),
            "Enabling the modules from Desktop must bind the root agent-up.json runtime sections.");
        using var created = await Server.Client.PostAsJsonAsync("/api/workspaces", request);
        if (!created.IsSuccessStatusCode)
            Assert.Fail($"Register workspace failed ({(int)created.StatusCode}): {await created.Content.ReadAsStringAsync()}");

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var workspaceId = body.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("The Server did not return a workspace id.");
        Stage($"Registered workspace {workspaceId}; starting it from Desktop.");

        var startError = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Desktop.ViewModel.Sidebar.LoadAsync();
            var workspace = Desktop.ViewModel.Sidebar.Workspaces.FirstOrDefault(item => item.Id == workspaceId);
            if (workspace is not null)
                Desktop.ViewModel.Sidebar.SelectedWorkspace = workspace;
            await Desktop.ViewModel.Sidebar.StartWorkspaceAsync(workspaceId);
            return Desktop.ViewModel.Sidebar.ErrorMessage;
        });
        if (!string.IsNullOrWhiteSpace(startError))
            Assert.Fail($"{startError}{await FormatWorkspaceOutputAsync(workspaceId)}");

        Stage("Desktop started the workspace; waiting for its applications.");
        await WaitForWorkspaceApplicationsAsync(workspaceId);
        await WaitForAllocatedHttpPortAsync(workspaceId, "Docs");
        await WaitForAllocatedHttpPortAsync(workspaceId, ExampleWeb);
        var apiPort = await WaitForAllocatedHttpPortAsync(workspaceId, ExampleApi);
        using (var health = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{apiPort}") })
            (await WaitForHealthyAsync(health)).Dispose();
        await WaitForApplicationTabAsync(ExampleWeb);
        await Desktop.WaitForWorkspaceWebViewAsync(TimeSpan.FromMinutes(2));
        Stage($"Root agent-up.json workspace is running after {started.Elapsed}.");
        return workspaceId;
    }

    // Most of this fixture runs before the first assertion, so when the run dies inside it the
    // only account of how far it got is what was printed on the way. Progress alone is not enough:
    // it reaches the console only at detailed verbosity, which is why the CI job asks for it.
    private static void Stage(string message)
    {
        TestContext.Progress.WriteLine(message);
        TestContext.Out.WriteLine(message);
    }

    private static async Task<int> WaitForAllocatedHttpPortAsync(string workspaceId, string application)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(12);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await Server!.Client.GetAsync($"/api/workspaces/{Uri.EscapeDataString(workspaceId)}");
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var allocated = document.RootElement.GetProperty("applications").EnumerateArray()
                    .Where(app => string.Equals(app.GetProperty("name").GetString(), application, StringComparison.Ordinal))
                    .SelectMany(app => app.GetProperty("allocatedPorts").EnumerateArray())
                    .FirstOrDefault(port => string.Equals(
                        port.GetProperty("protocol").GetString(),
                        "http",
                        StringComparison.OrdinalIgnoreCase));
                if (allocated.ValueKind != JsonValueKind.Undefined)
                    return allocated.GetProperty("allocatedPort").GetInt32();
            }

            await Task.Delay(500);
        }

        Assert.Fail($"Workspace '{workspaceId}' never allocated an HTTP port for '{application}'.");
        return 0;
    }

    private static async Task<HttpResponseMessage> WaitForHealthyAsync(HttpClient client)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(3);
        HttpResponseMessage? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            last?.Dispose();
            try
            {
                last = await client.GetAsync("/health");
                if (last.IsSuccessStatusCode)
                    return last;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                TestContext.Progress.WriteLine(ex.Message);
            }

            await Task.Delay(500);
        }

        Assert.Fail($"Example API /health did not become ready. Last status was '{last?.StatusCode}'.");
        return last!;
    }

    private static void AssertEnabledOnUi(string id)
    {
        var modules = Desktop!.ViewModel.Modules
            ?? throw new InvalidOperationException("Desktop did not mount the capability modules catalog.");
        var module = modules.Modules.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        Assert.That(module, Is.Not.Null, $"Desktop catalog did not list '{id}' after enable. {modules.Status}");
        Assert.That(module!.Enabled, Is.True, $"Desktop did not enable '{id}'. {modules.Status}");
    }

    private static async Task<string> FormatWorkspaceOutputAsync(string workspaceId)
    {
        var blocks = new List<string>();
        foreach (var application in WorkspaceApplications)
        {
            using var response = await Server!.Client.GetAsync(
                $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(application)}/output");
            if (!response.IsSuccessStatusCode)
                continue;

            var lines = await response.Content.ReadFromJsonAsync<string[]>() ?? [];
            if (lines.Length == 0)
                continue;

            blocks.Add($"{application} console:{Environment.NewLine}"
                + string.Join(Environment.NewLine, lines.TakeLast(40)));
        }

        return blocks.Count == 0 ? "" : Environment.NewLine + string.Join(Environment.NewLine, blocks);
    }

    private static async Task WaitForApplicationTabAsync(string application)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var selected = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var viewModel = Desktop!.ViewModel;
                var tab = viewModel.Applications.Applications
                    .FirstOrDefault(item => string.Equals(item.Name, application, StringComparison.Ordinal));
                if (tab is null)
                    return false;

                viewModel.SelectedApplicationTab = tab;
                return true;
            });
            if (selected)
                return;

            await Task.Delay(500);
        }

        Assert.Fail($"Desktop never listed application '{application}' after starting the root workspace.");
    }

    private static async Task WaitForWorkspaceApplicationsAsync(string workspaceId)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(12);
        HashSet<string> names = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await Server!.Client.GetAsync($"/api/workspaces/{Uri.EscapeDataString(workspaceId)}");
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                names = document.RootElement.GetProperty("applications")
                    .EnumerateArray()
                    .Select(app => app.GetProperty("name").GetString() ?? "")
                    .Where(name => name.Length > 0)
                    .ToHashSet(StringComparer.Ordinal);
                if (WorkspaceApplications.All(names.Contains))
                    return;
            }

            await Task.Delay(500);
        }

        Assert.Fail(
            "Root agent-up.json did not start every declared application. "
            + $"Expected [{string.Join(", ", WorkspaceApplications)}], saw [{string.Join(", ", names.Order(StringComparer.Ordinal))}].");
    }

    private static string PackRuntimeCapabilities(string repoRoot)
    {
        var fromEnv = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_REGISTRY_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(Path.Join(fromEnv, "index.json")))
            return fromEnv;

        RegistryRoot = Path.Join(Path.GetTempPath(), "agentup-e2e-registry-" + Guid.NewGuid().ToString("N"));
        var script = Path.Join(repoRoot, "scripts", "pack-first-party-capabilities.sh");
        var start = new ProcessStartInfo
        {
            FileName = "bash",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add(RegistryRoot);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the first-party capability packer.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            Assert.Fail($"Packing runtime capabilities failed ({process.ExitCode}): {stdout}{stderr}");
        return RegistryRoot;
    }

    private static string? FindRepositoryRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Join(directory, "agent-up.sln"))
                && File.Exists(Path.Join(directory, "agent-up.json")))
                return directory;

            var parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
                break;
            directory = parent;
        }

        return null;
    }
}
