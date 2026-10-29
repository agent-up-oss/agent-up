using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Training.Controllers;
using AgentUp.Server.Features.Training.DTOs;
using AgentUp.Server.Features.Training.Interfaces;
using AgentUp.Server.Features.Training.Models;
using AgentUp.Server.Features.Training.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.Training.HTTP;

/// <summary>
/// The endpoints the Train tab reads and writes: the repository tiles it opens on, the cards it
/// swipes through, the verdict it sends back, and the file it exports.
/// </summary>
[TestFixture]
public sealed class TrainingHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task Repositories_areEmptyBeforeAnyAgentHasDecidedAnything()
    {
        await using var host = await HostAsync();

        var tiles = await host.Client.GetFromJsonAsync<List<DecisionRepositoryDto>>("/api/training/repositories", Json);

        Assert.That(tiles, Is.Empty);
    }

    [Test]
    public async Task ARepositoryTile_countsWhatIsWaitingInEachCategory()
    {
        await using var host = await HostAsync();
        await RecordAsync(host, "product-vision", "Queue the work on the Server rather than the host");
        await RecordAsync(host, "gui-decisions", "Put the reorder grip on the left of each queued row");
        await RecordAsync(host, "gui-decisions", "Crop a long task title rather than wrapping it");

        var tiles = await host.Client.GetFromJsonAsync<List<DecisionRepositoryDto>>("/api/training/repositories", Json);

        var tile = tiles!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(tile.Owner, Is.EqualTo("acme"));
            Assert.That(tile.Repository, Is.EqualTo("widgets"));
            Assert.That(tile.Pending, Is.EqualTo(3));
            Assert.That(tile.Reviewed, Is.Zero);
            Assert.That(tile.Categories.Single(c => c.Category == "gui-decisions").Pending, Is.EqualTo(2));
            Assert.That(tile.Categories.Single(c => c.Category == "code-architecture").Pending, Is.Zero);
        });
    }

    [Test]
    public async Task EveryCategoryIsOnTheTile_evenTheEmptyOnes()
    {
        // The tile is how a reader sees where the backlog is not, as well as where it is.
        await using var host = await HostAsync();
        await RecordAsync(host, "product-vision", "Queue the work on the Server rather than the host");

        var tiles = await host.Client.GetFromJsonAsync<List<DecisionRepositoryDto>>("/api/training/repositories", Json);

        Assert.That(
            tiles!.Single().Categories.Select(category => category.Category),
            Is.EqualTo(new[] { "product-vision", "gui-decisions", "code-architecture" }));
    }

    [Test]
    public async Task Decisions_narrowToOneCategory()
    {
        await using var host = await HostAsync();
        await RecordAsync(host, "product-vision", "Queue the work on the Server rather than the host");
        await RecordAsync(host, "gui-decisions", "Put the reorder grip on the left of each queued row");

        var gui = await host.Client.GetFromJsonAsync<List<ProductDecisionDto>>(
            "/api/training/decisions?owner=acme&repository=widgets&category=gui-decisions", Json);

        Assert.That(gui!.Single().Headline, Is.EqualTo("Put the reorder grip on the left of each queued row"));
    }

    [Test]
    public async Task AVerdictComesBackOnTheDecisionItJudged()
    {
        await using var host = await HostAsync();
        var recorded = await RecordAsync(host, "product-vision", "Queue the work on the Server rather than the host");

        var response = await host.Client.PostAsJsonAsync(
            $"/api/training/decisions/{recorded!.Decision!.Id}/review",
            new ReviewDecisionRequest("good", "Right call, keep it."),
            Json);
        var reviewed = await response.Content.ReadFromJsonAsync<ProductDecisionDto>(Json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reviewed!.Verdict, Is.EqualTo("good"));
            Assert.That(reviewed.Note, Is.EqualTo("Right call, keep it."));
            Assert.That(reviewed.ReviewedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task AVerdictOnSomethingThatIsNotThere_isRefused()
    {
        await using var host = await HostAsync();

        var response = await host.Client.PostAsJsonAsync(
            "/api/training/decisions/nope/review", new ReviewDecisionRequest("good"), Json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task AVerdictNobodyCanGive_isRefused()
    {
        await using var host = await HostAsync();
        var recorded = await RecordAsync(host, "product-vision", "Queue the work on the Server rather than the host");

        var response = await host.Client.PostAsJsonAsync(
            $"/api/training/decisions/{recorded!.Decision!.Id}/review",
            new ReviewDecisionRequest("maybe"),
            Json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task AMalformedDecision_isRefusedWithWhatIsWrongWithIt()
    {
        await using var host = await HostAsync();

        var response = await host.Client.PostAsJsonAsync(
            "/api/training/decisions",
            new RecordDecisionRequest("ws-1", "product-vision", "no", "no", "no"),
            Json);
        var result = await response.Content.ReadFromJsonAsync<RecordDecisionResult>(Json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(result!.Recorded, Is.False);
            Assert.That(result.Faults, Is.Not.Empty);
        });
    }

    [Test]
    public async Task TheExport_carriesTheJudgedDecisionsOneToALine()
    {
        await using var host = await HostAsync();
        var first = await RecordAsync(host, "product-vision", "Queue the work on the Server rather than the host");
        await RecordAsync(host, "gui-decisions", "Put the reorder grip on the left of each queued row");
        await host.Client.PostAsJsonAsync(
            $"/api/training/decisions/{first!.Decision!.Id}/review",
            new ReviewDecisionRequest("good", "Right call."),
            Json);

        var response = await host.Client.GetAsync("/api/training/export");
        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Multiple(() =>
        {
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/x-ndjson"));
            Assert.That(lines, Has.Length.EqualTo(1), "a decision nobody judged teaches nothing");
            Assert.That(lines[0], Does.Contain("\"verdict\":\"good\"").And.Contain("acme/widgets"));
        });
    }

    [Test]
    public async Task TheExport_canCarryTheUnjudgedOnesToo()
    {
        await using var host = await HostAsync();
        await RecordAsync(host, "product-vision", "Queue the work on the Server rather than the host");

        var body = await host.Client.GetStringAsync("/api/training/export?includePending=true");

        Assert.That(body.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(1));
    }

    private static async Task<RecordDecisionResult?> RecordAsync(Host host, string category, string headline)
    {
        var response = await host.Client.PostAsJsonAsync(
            "/api/training/decisions",
            new RecordDecisionRequest(
                "ws-1",
                category,
                headline,
                "What was decided, said at enough length to be acted on later.",
                "What the call was based on, said at enough length to be worth reading.",
                Agent: "Lucy"),
            Json);
        return await response.Content.ReadFromJsonAsync<RecordDecisionResult>(Json);
    }

    private static async Task<Host> HostAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = ["--urls=http://127.0.0.1:0"] });
        builder.Services.AddControllers().AddApplicationPart(typeof(TrainingHttpController).Assembly);
        builder.Services.AddSingleton<IProductDecisionStore, MemoryDecisionStore>();
        builder.Services.AddSingleton<IDecisionRepositoryResolver, FixedRepository>();
        builder.Services.AddSingleton<ProductDecisionService>();
        builder.Services.AddSingleton<TrainingService>();
        builder.Services.AddSingleton<TrainingController>();

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        return new Host(app, new HttpClient { BaseAddress = new Uri(app.Urls.First()) });
    }

    private sealed record Host(WebApplication App, HttpClient Client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }

    private sealed class FixedRepository : IDecisionRepositoryResolver
    {
        public (string Owner, string Repository)? Resolve(string workspaceId) => ("acme", "widgets");
    }

    private sealed class MemoryDecisionStore : IProductDecisionStore
    {
        private IReadOnlyList<ProductDecision> _decisions = [];

        public IReadOnlyList<ProductDecision> Read() => _decisions;

        public void Write(IReadOnlyList<ProductDecision> decisions) => _decisions = [.. decisions];
    }
}
