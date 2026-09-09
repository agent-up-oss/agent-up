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
using AgentUp.Desktop.Features.Database.Controllers;
using AgentUp.Desktop.Features.Database.Providers;
using AgentUp.Desktop.Features.Database.Services;
using AgentUp.Desktop.Features.Database.ViewModels;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.Metrics.Controllers;
using AgentUp.Desktop.Features.Metrics.Providers;
using AgentUp.Desktop.Features.Metrics.Services;
using AgentUp.Desktop.Features.Metrics.ViewModels;
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
    private static readonly HttpClient DefaultAuthHttpClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:5000")
    };

    private static readonly HttpClient DefaultAuditHttpClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:5000")
    };

    private static readonly HttpClient DefaultMetricsHttpClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:5000")
    };

    private static readonly HttpClient DefaultDatabaseHttpClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:5000")
    };

    public static MainViewModel Create(
        WorkspaceApiClient workspaceClient,
        ConsoleApiClient consoleClient,
        MetricsApiClient? metricsClient = null,
        DatabaseApiClient? databaseClient = null,
        ApplicationAuditApiClient? auditClient = null,
        FirstRunTutorialViewModel? tutorial = null,
        LoginViewModel? login = null)
    {
        var workspaces = new WorkspacesController(new WorkspaceListService(workspaceClient));
        var applications = new ApplicationsController(new ApplicationSelectionService());
        var console = new ConsoleController(new ConsoleOutputService(consoleClient));
        var metrics = new MetricsController(new MetricsTimelineService(
            metricsClient ?? new MetricsApiClient(DefaultMetricsHttpClient)));
        var database = new DatabaseController(new DatabaseExplorerService(
            databaseClient ?? new DatabaseApiClient(DefaultDatabaseHttpClient)));
        var ports = new PortsController(new PortTabService());
        var audit = new ApplicationAuditController(new ApplicationAuditService(
            auditClient ?? new ApplicationAuditApiClient(DefaultAuditHttpClient)));

        return new MainViewModel(
            new WorkspaceListViewModel(workspaces),
            new ApplicationListViewModel(applications),
            new ConsoleViewModel(console),
            new MetricsViewModel(metrics),
            new DatabaseViewModel(database),
            new ApplicationAuditViewModel(audit),
            tutorial ?? new FirstRunTutorialViewModel(
                new FileFirstRunTutorialSettingsStore(),
                new FirstRunTutorialChecks(workspaces, new FirstRunProcessProvider())),
            login ?? new LoginViewModel(new AuthenticationController(new AuthenticationService(
                new AuthenticationApiClient(DefaultAuthHttpClient)))),
            ports);
    }

    public static MainViewModel Create(HttpClient http, LoginViewModel? login = null)
    {
        return Create(
            new WorkspaceApiClient(http),
            new ConsoleApiClient(http),
            new MetricsApiClient(http),
            new DatabaseApiClient(http),
            new ApplicationAuditApiClient(http),
            login: login ?? new LoginViewModel(new AuthenticationController(new AuthenticationService(
                new AuthenticationApiClient(http)))));
    }

    public static HostMetricsController CreateHostMetricsController(HttpClient http) =>
        new(new HostMetricsReporter(new HostMetricsApiClient(http)));
}
