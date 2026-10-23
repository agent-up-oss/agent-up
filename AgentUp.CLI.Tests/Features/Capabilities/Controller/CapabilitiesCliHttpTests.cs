using System.Text.Json.Nodes;
using AgentUp.CLI.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace AgentUp.CLI.Tests.Features.Capabilities.Controller;

[TestFixture]
public sealed class CapabilitiesCliHttpTests
{
    [Test]
    public async Task Capabilities_withoutAVerb_printsHelpThroughTheCliRunner()
    {
        using var output = new StringWriter();

        var code = await CliRunnerFactory.Create("http://localhost:1", Directory.GetCurrentDirectory(), output)
            .RunAsync(["capabilities"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("list"));
            Assert.That(output.ToString(), Does.Contain("bootstrap"));
        });
    }

    [Test]
    public async Task List_againstALiveServer_printsEnabledStateAndTheNixReason()
    {
        await using var host = await StartAsync(enabled: true, canRun: false, "https://nixos.org/download");
        using var output = new StringWriter();

        var code = await CliRunnerFactory.Create(host.Url, Directory.GetCurrentDirectory(), output)
            .RunAsync(["capabilities", "list"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("dotnet"));
            Assert.That(output.ToString(), Does.Contain("enabled=true"));
            Assert.That(output.ToString(), Does.Contain("canRun=false"));
            Assert.That(output.ToString(), Does.Contain("https://nixos.org/download"));
            Assert.That(output.ToString(), Does.Not.Contain("is not enabled"));
        });
    }

    [Test]
    public async Task Enable_againstALiveServer_enablesThePackedModule()
    {
        await using var host = await StartAsync(enabled: false, canRun: false, "Capability 'dotnet' is not enabled.");
        using var output = new StringWriter();

        var code = await CliRunnerFactory.Create(host.Url, Directory.GetCurrentDirectory(), output)
            .RunAsync(["capabilities", "enable", "dotnet"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("Enabled dotnet 1.0.0."));
            Assert.That(host.Enabled, Is.True);
        });
    }

    private static async Task<Host> StartAsync(bool enabled, bool canRun, string message)
    {
        var state = new JsonObject
        {
            ["id"] = "dotnet",
            ["version"] = "1.0.0",
            ["displayName"] = ".NET",
            ["publisher"] = "agent-up",
            ["kind"] = "runtime",
            ["enabled"] = enabled,
            ["state"] = enabled ? "ready" : "disabled",
            ["canRun"] = canRun,
            ["messages"] = new JsonArray(message)
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapGet("/api/capabilities", () => Results.Json(new[] { state }));
        app.MapPost("/api/capabilities/enable", () =>
        {
            state["enabled"] = true;
            state["state"] = "ready";
            state["canRun"] = true;
            state["messages"] = new JsonArray();
            return Results.Json(state);
        });
        await app.StartAsync();
        return new Host(app, app.Urls.Single().TrimEnd('/'), state);
    }

    private sealed class Host(WebApplication app, string url, JsonObject state) : IAsyncDisposable
    {
        public string Url { get; } = url;

        public bool Enabled => state["enabled"]?.GetValue<bool>() == true;

        public async ValueTask DisposeAsync() => await app.DisposeAsync();
    }
}
