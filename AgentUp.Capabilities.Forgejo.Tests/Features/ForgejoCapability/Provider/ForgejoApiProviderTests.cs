using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;
using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Forgejo.Tests.Features.ForgejoCapability.Provider;

[TestFixture]
public sealed class ForgejoApiProviderTests
{
    [Test]
    public async Task ReadAsync_returns_proposal_commit_statuses_threads_and_mergeability()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", Statuses),
            ("GET /repos/acme/widgets/pulls/42/comments", Comments));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Proposal!.Number, Is.EqualTo("42"));
            Assert.That(snapshot.Checks.Single().Name, Is.EqualTo("ci"));
            Assert.That(snapshot.Checks.Single().Conclusion, Is.EqualTo("success"));
            Assert.That(snapshot.Threads.Single().Id, Is.EqualTo("99"));
            Assert.That(snapshot.Mergeability!.Mergeable, Is.True);
        });
    }

    [Test]
    public async Task ReadAsync_returns_empty_when_no_proposal_is_open()
    {
        var snapshot = await new ForgejoForgeCapability(Recorded(("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", "[]")))
            .ReadAsync(Request());

        Assert.That(snapshot.Proposal, Is.Null);
        Assert.That(snapshot.Checks, Is.Empty);
    }

    [Test]
    public async Task ListAssignedAsync_maps_stable_ids()
    {
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/issues?state=open&assigned=true&type=issues&limit=100", Assigned)))
            .ListAssignedAsync(Request());

        Assert.That(issues.Single().Number, Is.EqualTo(3));
        Assert.That(issues.Single().Id, Is.EqualTo("7"));
    }

    [Test]
    public async Task ListAssignedAsync_returnsEmptyWhenThePayloadIsNotAnArray()
    {
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/issues?state=open&assigned=true&type=issues&limit=100", "{}")))
            .ListAssignedAsync(Request());

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task ListIssuesAsync_readsTheRepositoryWithoutTheAssignedFilter()
    {
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/issues?state=open&type=issues&sort=recentupdate&limit=100", Assigned)))
            .ListIssuesAsync(Request());

        Assert.That(issues.Single().Number, Is.EqualTo(3));
        Assert.That(issues.Single().Id, Is.EqualTo("7"));
    }

    [Test]
    public async Task ListIssuesAsync_returnsEmptyWhenThePayloadIsNotAnArray()
    {
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/issues?state=open&type=issues&sort=recentupdate&limit=100", "{}")))
            .ListIssuesAsync(Request());

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task ReadAsync_skipsStatusesWhenTheHeadShaIsMissing()
    {
        const string pulls = """
            [{"id":1,"number":42,"title":"Topic","html_url":"https://forgejo.example.test/acme/widgets/pulls/42","state":"open","mergeable":true,"mergeable_state":"clean","head":{"ref":"topic","sha":""},"base":{"ref":"main"}}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", pulls),
            ("GET /repos/acme/widgets/pulls/42/comments", "{}"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Checks, Is.Empty);
        Assert.That(snapshot.Threads, Is.Empty);
        Assert.That(handler.Requests, Has.None.Contains("statuses"));
    }

    [Test]
    public async Task ReadAsync_mapsPendingStatusesToInProgress()
    {
        const string pending = """
            [{"id":1,"context":"ci","status":"pending","target_url":"https://forgejo.example.test/acme/widgets/actions/1"}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", pending),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Checks.Single().Status, Is.EqualTo("in_progress"));
        Assert.That(snapshot.Checks.Single().Conclusion, Is.EqualTo(""));
    }

    [Test]
    public async Task ReadAsync_acceptsStateWhenStatusIsMissing()
    {
        const string statuses = """
            [{"id":1,"context":"ci","state":"failure","target_url":"https://forgejo.example.test/acme/widgets/actions/1"}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", statuses),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Checks.Single().Conclusion, Is.EqualTo("failure"));
        Assert.That(snapshot.Checks.Single().Status, Is.EqualTo("completed"));
    }

    [Test]
    public async Task ReadAsync_returnsEmptyChecksWhenStatusesAreNotAnArray()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "{}"),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Checks, Is.Empty);
        Assert.That(snapshot.Threads, Is.Empty);
    }

    [Test]
    public async Task OpenProposalAsync_posts_the_recorded_pull()
    {
        var proposal = await new ForgejoForgeCapability(Recorded(("POST /repos/acme/widgets/pulls", Opened)))
            .OpenProposalAsync(Request(), new ForgeOpenProposalCommand("Title", "Body", "topic", "main"));

        Assert.That(proposal.Number, Is.EqualTo("42"));
        Assert.That(proposal.Url, Does.Contain("pulls/42"));
    }

    [Test]
    public void UpdateRefAsync_refuses_a_protected_ref_without_calling_the_host()
    {
        var handler = Recorded();

        var error = Assert.ThrowsAsync<ForgeProtectedRefException>(() =>
            new ForgejoForgeCapability(handler).UpdateRefAsync(Request(), new ForgeUpdateRefCommand("main", "abc")));
        Assert.That(error!.Ref, Is.EqualTo("main"));
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task UpdateRefAsync_patches_an_unprotected_ref()
    {
        var handler = Recorded(("PATCH /repos/acme/widgets/git/refs/heads/topic", "{}"));

        await new ForgejoForgeCapability(handler).UpdateRefAsync(Request(), new ForgeUpdateRefCommand("topic", "abc"));

        Assert.That(handler.Requests.Single(), Does.Contain("PATCH /repos/acme/widgets/git/refs/heads/topic"));
    }

    [Test]
    public async Task CommentAsync_posts_to_the_issue()
    {
        var handler = Recorded(("POST /repos/acme/widgets/issues/42/comments", "{}"));

        await new ForgejoForgeCapability(handler).CommentAsync(Request(), new ForgeCommentCommand("42", "noted"));

        Assert.That(handler.Requests.Single(), Does.Contain("issues/42/comments"));
    }

    [Test]
    public async Task CreateClient_uses_the_configured_api_base_url()
    {
        var host = ForgejoHostConfigurationProvider.From("https://forgejo.example.test", null, null, null);
        var handler = Recorded(("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", "[]"));

        await new ForgejoForgeCapability(handler, host).ReadAsync(Request());

        Assert.That(handler.Uris.Single(), Does.StartWith("https://forgejo.example.test/api/v1/repos/"));
    }

    [Test]
    public void HostConfiguration_derives_endpoints_from_origin()
    {
        var host = ForgejoHostConfigurationProvider.From("https://git.example.test/", null, null, null);

        Assert.That(host.ApiBaseUrl, Is.EqualTo("https://git.example.test/api/v1/"));
        Assert.That(host.OAuthAuthorizeUrl, Is.EqualTo("https://git.example.test/login/oauth/authorize"));
        Assert.That(host.OAuthTokenUrl, Is.EqualTo("https://git.example.test/login/oauth/access_token"));
    }

    [Test]
    public void HostConfiguration_overrides_each_endpoint_independently()
    {
        var host = ForgejoHostConfigurationProvider.From(
            "https://git.example.test",
            "https://git.example.test/custom/api/",
            "https://git.example.test/custom/authorize",
            "https://git.example.test/custom/token");

        Assert.That(host.ApiBaseUrl, Is.EqualTo("https://git.example.test/custom/api/"));
        Assert.That(host.OAuthAuthorizeUrl, Is.EqualTo("https://git.example.test/custom/authorize"));
        Assert.That(host.OAuthTokenUrl, Is.EqualTo("https://git.example.test/custom/token"));
    }

    [Test]
    public void HostConfiguration_defaultsOriginWhenBlank()
    {
        var host = ForgejoHostConfigurationProvider.From("  ", null, null, null);

        Assert.That(host.ApiBaseUrl, Is.EqualTo(ForgejoHostConfigurationProvider.DefaultOrigin + "/api/v1/"));
        Assert.That(host.OAuthAuthorizeUrl, Does.StartWith(ForgejoHostConfigurationProvider.DefaultOrigin));
    }

    [Test]
    public void HostConfiguration_appendsATrailingSlashToApiBase()
        => Assert.That(
            ForgejoHostConfigurationProvider.ApiBase("https://git.example.test/api/v1"),
            Is.EqualTo("https://git.example.test/api/v1/"));

    [Test]
    public async Task ReadAsync_treatsMissingMergeabilityAsUnknown()
    {
        const string pulls = """
            [{"id":1,"number":42,"title":"Topic","html_url":"https://forgejo.example.test/acme/widgets/pulls/42","state":"open","head":{"ref":"topic","sha":"abc123"},"base":{"ref":"main"}}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Mergeability!.Mergeable, Is.False);
        Assert.That(snapshot.Mergeability.State, Is.EqualTo("unknown"));
    }

    [Test]
    public async Task ReadAsync_mapsStatusesWithoutContextOrUrl()
    {
        const string statuses = """
            [{"id":1}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", statuses),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Checks.Single().Name, Is.Empty);
        Assert.That(snapshot.Checks.Single().Url, Is.Empty);
        Assert.That(snapshot.Checks.Single().Status, Is.EqualTo("completed"));
    }

    [Test]
    public async Task SubmitReviewAsync_posts_a_review()
    {
        var handler = Recorded(("POST /repos/acme/widgets/pulls/42/reviews", "{}"));

        await new ForgejoForgeCapability(handler).SubmitReviewAsync(
            Request(),
            new ForgeSubmitReviewCommand("42", "lgtm", "COMMENT"));

        Assert.That(handler.Requests.Single(), Does.Contain("pulls/42/reviews"));
    }

    [Test]
    public void HandlerConstructor_usesOneResolvedHostForApiAndOAuth()
    {
        var previousOrigin = Environment.GetEnvironmentVariable("AGENTUP_FORGEJO_ORIGIN");
        var previousApi = Environment.GetEnvironmentVariable("AGENTUP_FORGEJO_API_URL");
        var previousAuthorize = Environment.GetEnvironmentVariable("AGENTUP_FORGEJO_AUTHORIZE_URL");
        var previousToken = Environment.GetEnvironmentVariable("AGENTUP_FORGEJO_TOKEN_URL");
        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_FORGEJO_ORIGIN", "https://git.example.test");
            Environment.SetEnvironmentVariable("AGENTUP_FORGEJO_API_URL", null);
            Environment.SetEnvironmentVariable("AGENTUP_FORGEJO_AUTHORIZE_URL", null);
            Environment.SetEnvironmentVariable("AGENTUP_FORGEJO_TOKEN_URL", null);

            using var handler = new HttpClientHandler();
            var capability = new ForgejoForgeCapability(handler);

            Assert.That(capability.Host.ApiBaseUrl, Is.EqualTo("https://git.example.test/api/v1/"));
            Assert.That(capability.Host.OAuthAuthorizeUrl, Is.EqualTo("https://git.example.test/login/oauth/authorize"));
            Assert.That(capability.Host.OAuthTokenUrl, Is.EqualTo("https://git.example.test/login/oauth/access_token"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_FORGEJO_ORIGIN", previousOrigin);
            Environment.SetEnvironmentVariable("AGENTUP_FORGEJO_API_URL", previousApi);
            Environment.SetEnvironmentVariable("AGENTUP_FORGEJO_AUTHORIZE_URL", previousAuthorize);
            Environment.SetEnvironmentVariable("AGENTUP_FORGEJO_TOKEN_URL", previousToken);
        }
    }

    [Test]
    public void Write_creates_capability_json()
    {
        var directory = Directory.CreateDirectory(
            Path.Join(Path.GetTempPath(), "agent-up-forgejo-write-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var packer = new ForgejoCapabilityPacker();
            new ForgejoCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
            Assert.That(File.ReadAllText(Path.Join(directory, "capability.json")), Does.Contain("forgejo"));
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
        public List<string> Uris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri is null ? "" : request.RequestUri.PathAndQuery;
            const string apiPrefix = "/api/v1";
            if (path.StartsWith(apiPrefix, StringComparison.Ordinal))
                path = path[apiPrefix.Length..];
            var key = $"{request.Method.Method} {path}";
            Requests.Add(key);
            if (request.RequestUri is not null)
                Uris.Add(request.RequestUri.ToString());
            if (!responses.TryGetValue(key, out var body))
                return Task.FromResult(CreateResponse(HttpStatusCode.NotFound));

            return Task.FromResult(CreateResponse(HttpStatusCode.OK, body));
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
        private static HttpResponseMessage CreateResponse(HttpStatusCode status, string? body = null)
            => body is null
                ? new(status)
                : new(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
    }

    private const string Pulls = """
        [{"id":1,"number":42,"title":"Topic","html_url":"https://forgejo.example.test/acme/widgets/pulls/42","state":"open","mergeable":true,"mergeable_state":"clean","head":{"ref":"topic","sha":"abc123"},"base":{"ref":"main"}}]
        """;

    private const string Statuses = """
        [{"id":1,"context":"ci","status":"success","target_url":"https://forgejo.example.test/acme/widgets/actions/1"}]
        """;

    private const string Comments = """
        [{"id":99,"html_url":"https://forgejo.example.test/acme/widgets/pulls/42#issuecomment-99","body":"please fix"}]
        """;

    private const string Assigned = """
        [{"id":7,"number":3,"title":"Assigned","body":"do it","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example.test/acme/widgets/issues/3"}]
        """;

    private const string Opened = """
        {"id":1,"number":42,"title":"Title","html_url":"https://forgejo.example.test/acme/widgets/pulls/42","state":"open","head":{"ref":"topic","sha":"abc123"},"base":{"ref":"main"}}
        """;
}
