using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.ServiceControl.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Tests.Features.ServiceControl.HTTP;

[TestFixture]
public class ServiceControlHttpTests
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        var port = FindFreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [$"--urls=http://localhost:{port}"]
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(ServiceControlController).Assembly);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.MapControllers();
        await _app.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        Environment.ExitCode = 0;
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task PostRestart_returnsAccepted()
    {
        var response = await _client.PostAsync("/api/service/restart", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
    }

    [Test]
    public async Task PostShutdown_returnsAccepted()
    {
        var response = await _client.PostAsync("/api/service/shutdown", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
    }

    private static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
