using System.Net;
using System.Text.Json;
using AgentUp.CLI.Composition;
using AgentUp.CLI.Features.Authentication.Controllers;
using AgentUp.CLI.Features.Authentication.DTOs;
using AgentUp.CLI.Features.Authentication.Providers;
using AgentUp.CLI.Features.Authentication.Services;
using AgentUp.CLI.Shared.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

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
        await using var server = await StartUnauthorizedWorkspaceServerAsync();
        var port = new Uri(server.Urls.First()).Port;

        using var output = new StringWriter();
        var exitCode = await CliRunnerFactory.Create($"http://localhost:{port}", Directory.GetCurrentDirectory(), output)
            .RunAsync(["list"]);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString().Trim(), Is.EqualTo(AuthenticationRequiredException.LoginHint));
        });
    }

    private static async Task<WebApplication> StartUnauthorizedWorkspaceServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
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
        await app.StartAsync();
        return app;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Join(Path.GetTempPath(), "AgentUp-CLI-Auth", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
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
