using AgentUp.Server.Composition;
using AgentUp.Server.Shared.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Tests.Support;

internal sealed class DesktopStreamingServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _dataDir;
    private readonly string? _previousRegistry;
    private readonly string? _previousEnabled;
    private readonly string? _previousAuth;

    private DesktopStreamingServer(
        WebApplication app,
        string dataDir,
        Uri baseUri,
        string? previousRegistry,
        string? previousEnabled,
        string? previousAuth)
    {
        _app = app;
        _dataDir = dataDir;
        _previousRegistry = previousRegistry;
        _previousEnabled = previousEnabled;
        _previousAuth = previousAuth;
        BaseUri = baseUri;
        this.Client = new HttpClient { BaseAddress = baseUri };
    }

    internal Uri BaseUri { get; }
    internal HttpClient Client { get; }

    internal T GetRequiredService<T>() where T : notnull => _app.Services.GetRequiredService<T>();

    internal static async Task<DesktopStreamingServer> StartAsync(
        string? capabilityRegistryPath = null,
        string? capabilityEnabledPath = null)
    {
        var dataDir = Path.Join(Path.GetTempPath(), "agentup-e2e-server-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);

        var previousAuth = Environment.GetEnvironmentVariable("AGENTUP_AUTH_DISABLED");
        var previousRegistry = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_REGISTRY_PATH");
        var previousEnabled = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_ENABLED_PATH");
        Environment.SetEnvironmentVariable("AGENTUP_AUTH_DISABLED", "true");
        if (capabilityRegistryPath is not null)
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_REGISTRY_PATH", capabilityRegistryPath);
        if (capabilityEnabledPath is not null)
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_ENABLED_PATH", capabilityEnabledPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls=http://127.0.0.1:0"]
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AGENTUP_AUTH_DISABLED"] = "true",
            ["Storage:DataDirectory"] = dataDir,
            ["AGENTUP_CAPABILITY_REGISTRY_PATH"] = capabilityRegistryPath,
            ["AGENTUP_CAPABILITY_ENABLED_PATH"] = capabilityEnabledPath
        });
        ServiceRegistration.Configure(builder, dataDir);

        var app = builder.Build();
        app.UseWebSockets();
        app.UseCors(WebClientOriginProvider.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        var url = app.Urls.Single().TrimEnd('/') + "/";
        var baseUri = new Uri(url);
        return new DesktopStreamingServer(app, dataDir, baseUri, previousRegistry, previousEnabled, previousAuth);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        Environment.SetEnvironmentVariable("AGENTUP_AUTH_DISABLED", _previousAuth);
        Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_REGISTRY_PATH", _previousRegistry);
        Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_ENABLED_PATH", _previousEnabled);
        try
        {
            Directory.Delete(_dataDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TestContext.Progress.WriteLine(ex.Message);
        }
    }
}
