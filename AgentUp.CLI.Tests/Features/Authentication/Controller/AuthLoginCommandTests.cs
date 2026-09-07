using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AgentUp.CLI.Composition;
using AgentUp.CLI.Features.Authentication.Controllers;
using AgentUp.CLI.Features.Authentication.DTOs;
using AgentUp.CLI.Features.Authentication.Providers;
using AgentUp.CLI.Features.Authentication.Services;
using AgentUp.CLI.Shared.Providers;
using Microsoft.AspNetCore.Builder;

namespace AgentUp.CLI.Tests.Features.Authentication.Controller;

[TestFixture]
public class AuthLoginCommandTests
{
    [Test]
    public async Task Login_storesToken_whenAuthenticationRequired()
    {
        var directory = CreateTempDirectory();
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/status")
            {
                return JsonResponse(new LoginResponse(true));
            }

            if (request.RequestUri.AbsolutePath == "/api/auth/login")
            {
                return JsonResponse(new LoginResponse(true, "access-token"));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var output = new StringWriter();
        var authenticationService = new AuthenticationService(
            new AuthenticationCredentialsStore(directory),
            "http://localhost",
            () => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") });
        var exitCode = await new AuthLoginCommand(
            new AuthenticationCommandService(authenticationService, new AuthenticationArgParser()),
            new AuthenticationOutputService(output)).RunAsync(["--password", "secret"]);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.ToString(), Does.Contain("Authenticated."));
            Assert.That(new AuthenticationCredentialsStore(directory).GetToken("http://localhost"), Is.EqualTo("access-token"));
        });
    }

    [Test]
    public async Task List_printsLoginHint_whenUnauthorized()
    {
        var port = FindFreePort();
        await using var server = BuildUnauthorizedWorkspaceServer(port);
        await server.StartAsync();

        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{port}", Directory.GetCurrentDirectory(), output)
            .RunAsync(["list"]);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString().Trim(), Is.EqualTo(AuthenticationRequiredException.LoginHint));
        });
    }

    private static WebApplication BuildUnauthorizedWorkspaceServer(int port)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [$"--urls=http://localhost:{port}"]
        });
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/workspaces"))
            {
                context.Response.StatusCode = 401;
                return;
            }

            await next(context);
        });
        return app;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Join(Path.GetTempPath(), "AgentUp-CLI-Auth", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload))
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response(request));
    }
}
