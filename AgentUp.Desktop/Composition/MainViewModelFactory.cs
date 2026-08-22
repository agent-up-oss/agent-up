using AgentUp.Desktop.Features.Applications.Controllers;
using AgentUp.Desktop.Features.Applications.Services;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Audit.Controllers;
using AgentUp.Desktop.Features.Audit.Providers;
using AgentUp.Desktop.Features.Audit.Services;
using AgentUp.Desktop.Features.Audit.ViewModels;
using AgentUp.Desktop.Features.Console.Controllers;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.Console.Services;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.FirstRun.Providers;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Ports.Controllers;
using AgentUp.Desktop.Features.Ports.Services;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Composition;

public static class MainViewModelFactory
{
    private static readonly HttpClient DefaultAuditHttpClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:5000")
    };

    public static MainViewModel Create(
        WorkspaceApiClient workspaceClient,
        ConsoleApiClient consoleClient,
        ApplicationAuditApiClient? auditClient = null,
        Func<string, string, string?, Task>? toggleControlMode = null,
        FirstRunTutorialViewModel? tutorial = null)
    {
        var workspaces = new WorkspacesController(new WorkspaceListService(workspaceClient));
        var applications = new ApplicationsController(new ApplicationSelectionService());
        var console = new ConsoleController(new ConsoleOutputService(consoleClient));
        var ports = new PortsController(new PortTabService());
        var audit = new ApplicationAuditController(new ApplicationAuditService(
            auditClient ?? new ApplicationAuditApiClient(DefaultAuditHttpClient)));

        return new MainViewModel(
            new WorkspaceListViewModel(workspaces, toggleControlMode),
            new ApplicationListViewModel(applications),
            new ConsoleViewModel(console),
            new ApplicationAuditViewModel(audit),
            tutorial ?? new FirstRunTutorialViewModel(
                new FileFirstRunTutorialSettingsStore(),
                new FirstRunTutorialChecks(workspaces, new FirstRunProcessProvider())),
            ports);
    }

    public static MainViewModel Create(string serverUrl)
    {
        var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        return Create(
            new WorkspaceApiClient(http),
            new ConsoleApiClient(http),
            new ApplicationAuditApiClient(http),
            toggleControlMode: async (workspaceId, authority, preset) =>
            {
                var presetQuery = string.IsNullOrWhiteSpace(preset)
                    ? string.Empty
                    : $"&preset={Uri.EscapeDataString(preset)}";
                using var _ = await http.PostAsync(
                    $"api/browser/input/control-mode/{workspaceId}?authority={authority}{presetQuery}",
                    content: null);
            });
    }
}
