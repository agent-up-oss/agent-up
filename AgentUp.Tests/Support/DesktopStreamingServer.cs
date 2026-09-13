using AgentUp.Server.Composition;
using AgentUp.Server.Shared.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Tests.Support;

internal sealed class DesktopStreamingServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _dataDir;

    private DesktopStreamingServer(WebApplication app, string dataDir, Uri baseUri)
    {
        _app = app;
        _dataDir = dataDir;
        BaseUri = baseUri;
        this.Client = new HttpClient { BaseAddress = baseUri };
    }

    internal Uri BaseUri { get; }
    internal HttpClient Client { get; }

    internal static async Task<DesktopStreamingServer> StartAsync()
    {
        var dataDir = Path.Join(Path.GetTempPath(), "agentup-e2e-server-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);

        Environment.SetEnvironmentVariable("AGENTUP_AUTH_DISABLED", "true");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls=http://127.0.0.1:0"]
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AGENTUP_AUTH_DISABLED"] = "true",
            ["Storage:DataDirectory"] = dataDir
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
        return new DesktopStreamingServer(app, dataDir, baseUri);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
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
