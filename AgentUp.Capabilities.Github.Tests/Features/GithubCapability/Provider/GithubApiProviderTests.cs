using System.Net;
using System.Text;
using AgentUp.Capabilities.Github.Features.GithubCapability.Providers;
using AgentUp.Capabilities.Github.Features.GithubCapability.Services;
using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Github.Tests.Features.GithubCapability.Provider;

[TestFixture]
public sealed class GithubApiProviderTests
{
    [Test]
    public async Task ReadAsync_returns_proposal_checks_threads_and_mergeability()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/check-runs", Checks),
            ("GET /repos/acme/widgets/pulls/42/comments", Comments));
        var snapshot = await new GithubForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Proposal!.Number, Is.EqualTo("42"));
            Assert.That(snapshot.Checks.Single().Conclusion, Is.EqualTo("success"));
            Assert.That(snapshot.Threads.Single().Id, Is.EqualTo("99"));
            Assert.That(snapshot.Mergeability!.Mergeable, Is.True);
        });
    }

    [Test]
    public async Task ReadAsync_returns_empty_when_no_proposal_is_open()
    {
        var snapshot = await new GithubForgeCapability(Recorded(("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", "[]")))
            .ReadAsync(Request());

        Assert.That(snapshot.Proposal, Is.Null);
        Assert.That(snapshot.Checks, Is.Empty);
    }

    [Test]
    public async Task ListAssignedAsync_maps_stable_ids()
    {
        var issues = await new GithubForgeCapability(Recorded(("GET /issues?filter=assigned&state=open&per_page=100", Assigned)))
            .ListAssignedAsync(Request());

        Assert.That(issues.Single().Number, Is.EqualTo(3));
        Assert.That(issues.Single().Id, Is.EqualTo("7"));
    }

    [Test]
    public async Task ListAssignedAsync_returnsEmptyWhenThePayloadIsNotAnArray()
    {
        var issues = await new GithubForgeCapability(Recorded(("GET /issues?filter=assigned&state=open&per_page=100", "{}")))
            .ListAssignedAsync(Request());

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task ReadAsync_skipsChecksWhenTheHeadShaIsMissing()
    {
        const string pulls = """
            [{"id":1,"number":42,"title":"Topic","html_url":"https://github.com/acme/widgets/pull/42","state":"open","mergeable":true,"mergeable_state":"clean","head":{"ref":"topic","sha":""},"base":{"ref":"main"}}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", pulls),
            ("GET /repos/acme/widgets/pulls/42/comments", "{}"));
        var snapshot = await new GithubForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Checks, Is.Empty);
        Assert.That(snapshot.Threads, Is.Empty);
        Assert.That(handler.Requests, Has.None.Contains("check-runs"));
    }

    [Test]
    public async Task ReadAsync_returnsEmptyChecksWhenCheckRunsAreMissing()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/check-runs", "{}"),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"));
        var snapshot = await new GithubForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Checks, Is.Empty);
        Assert.That(snapshot.Threads, Is.Empty);
    }

    [Test]
    public async Task OpenProposalAsync_posts_the_recorded_pull()
    {
        var proposal = await new GithubForgeCapability(Recorded(("POST /repos/acme/widgets/pulls", Opened)))
            .OpenProposalAsync(Request(), new ForgeOpenProposalCommand("Title", "Body", "topic", "main"));

        Assert.That(proposal.Number, Is.EqualTo("42"));
        Assert.That(proposal.Url, Does.Contain("pull/42"));
    }

    [Test]
    public void UpdateRefAsync_refuses_a_protected_ref_without_calling_the_host()
    {
        var handler = Recorded();

        var error = Assert.ThrowsAsync<ForgeProtectedRefException>(() =>
            new GithubForgeCapability(handler).UpdateRefAsync(Request(), new ForgeUpdateRefCommand("main", "abc")));
        Assert.That(error!.Ref, Is.EqualTo("main"));
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task UpdateRefAsync_patches_an_unprotected_ref()
    {
        var handler = Recorded(("PATCH /repos/acme/widgets/git/refs/heads/topic", "{}"));

        await new GithubForgeCapability(handler).UpdateRefAsync(Request(), new ForgeUpdateRefCommand("topic", "abc"));

        Assert.That(handler.Requests.Single(), Does.Contain("PATCH /repos/acme/widgets/git/refs/heads/topic"));
    }

    [Test]
    public async Task CommentAsync_posts_to_the_issue()
    {
        var handler = Recorded(("POST /repos/acme/widgets/issues/42/comments", "{}"));

        await new GithubForgeCapability(handler).CommentAsync(Request(), new ForgeCommentCommand("42", "noted"));

        Assert.That(handler.Requests.Single(), Does.Contain("issues/42/comments"));
    }

    [Test]
    public async Task SubmitReviewAsync_posts_a_review()
    {
        var handler = Recorded(("POST /repos/acme/widgets/pulls/42/reviews", "{}"));

        await new GithubForgeCapability(handler).SubmitReviewAsync(
            Request(),
            new ForgeSubmitReviewCommand("42", "lgtm", "COMMENT"));

        Assert.That(handler.Requests.Single(), Does.Contain("pulls/42/reviews"));
    }

    [Test]
    public void Write_creates_capability_json()
    {
        var directory = Directory.CreateDirectory(
            Path.Join(Path.GetTempPath(), "agent-up-github-write-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var packer = new GithubCapabilityPacker();
            new GithubCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
            Assert.That(File.ReadAllText(Path.Join(directory, "capability.json")), Does.Contain("forge"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ForgeRequest Request() => new("token", "acme", "widgets", "topic");

    private static RecordedHandler Recorded(params (string Key, string Body)[] responses)
        => new(responses.ToDictionary(pair => pair.Key, pair => pair.Body, StringComparer.Ordinal));

    private sealed class RecordedHandler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri is null
                ? ""
                : request.RequestUri.PathAndQuery.TrimStart('/');
            var key = $"{request.Method.Method} /{path}";
            Requests.Add(key);
            if (!responses.TryGetValue(key, out var body))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private const string Pulls = """
        [{"id":1,"number":42,"title":"Topic","html_url":"https://github.com/acme/widgets/pull/42","state":"open","mergeable":true,"mergeable_state":"clean","head":{"ref":"topic","sha":"abc123"},"base":{"ref":"main"}}]
        """;

    private const string Checks = """
        {"check_runs":[{"name":"ci","status":"completed","conclusion":"success","html_url":"https://github.com/acme/widgets/runs/1"}]}
        """;

    private const string Comments = """
        [{"id":99,"html_url":"https://github.com/acme/widgets/pull/42#discussion_r99","body":"please fix"}]
        """;

    private const string Assigned = """
        [{"id":7,"number":3,"title":"Assigned","body":"do it","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://github.com/acme/widgets/issues/3"}]
        """;

    private const string Opened = """
        {"id":1,"number":42,"title":"Title","html_url":"https://github.com/acme/widgets/pull/42","state":"open","head":{"ref":"topic","sha":"abc123"},"base":{"ref":"main"}}
        """;
}
