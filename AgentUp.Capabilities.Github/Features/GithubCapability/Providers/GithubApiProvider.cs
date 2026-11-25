using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Github.Features.GithubCapability.Providers;

/// <summary>
/// Talks to the GitHub REST API. The handler is injectable so tests can replay recorded
/// responses without a live account.
/// </summary>
public sealed class GithubApiProvider(HttpMessageHandler? handler = null, string? apiBaseUrl = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _apiBaseUrl = GithubHostConfigurationProvider.ApiBase(apiBaseUrl);

    public async Task<ForgeHostSnapshot> ReadAsync(ForgeRequest request, CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        var account = await ReadAccountAsync(client, cancellationToken);
        var pulls = await client.GetFromJsonAsync<JsonElement>(
            $"repos/{request.Owner}/{request.Repository}/pulls?head={request.Owner}:{request.Branch}&state=open",
            Json,
            cancellationToken);
        if (pulls.ValueKind != JsonValueKind.Array || pulls.GetArrayLength() == 0)
            return new ForgeHostSnapshot(null, [], [], null, account);

        var pull = pulls[0];
        var proposal = ReadProposal(pull);
        var sha = pull.GetProperty("head").GetProperty("sha").GetString() ?? "";
        var checks = await ReadChecksAsync(client, request, sha, cancellationToken);
        var threads = await ReadThreadsAsync(client, request, proposal.Number, cancellationToken);
        var reviews = await ReadReviewsAsync(client, request, proposal.Number, cancellationToken);
        var mergeability = new ForgeMergeability(
            pull.TryGetProperty("mergeable", out var mergeable) && mergeable.ValueKind == JsonValueKind.True,
            pull.TryGetProperty("mergeable_state", out var state) ? state.GetString() ?? "unknown" : "unknown");
        return new ForgeHostSnapshot(proposal, checks, threads, mergeability, account, reviews);
    }

    /// <summary>
    /// The verdicts left on the proposal, one per reviewer. A host keeps every review a reviewer
    /// ever submitted, so only their latest decisive one counts: an approval after changes were
    /// requested means the changes landed. A plain comment is not decisive — the host leaves an
    /// approval or a change request standing when the same reviewer comments again afterwards —
    /// so a reviewer who approved and then commented still reads as approved, and one who asked
    /// for changes still blocks.
    /// </summary>
    private static async Task<IReadOnlyList<ForgeReview>> ReadReviewsAsync(
        HttpClient client,
        ForgeRequest request,
        string proposalNumber,
        CancellationToken cancellationToken)
    {
        var reviews = await client.GetFromJsonAsync<JsonElement>(
            // One page is 30 by default, and a reviewer's newest review on page two would be lost
            // from the per-reviewer verdict, so ask for the largest page the host serves.
            $"repos/{request.Owner}/{request.Repository}/pulls/{proposalNumber}/reviews?per_page=100",
            Json,
            cancellationToken);
        if (reviews.ValueKind != JsonValueKind.Array) return [];

        return reviews.EnumerateArray()
            .Select(ReadReview)
            .OfType<ForgeReview>()
            .GroupBy(review => review.Login, StringComparer.OrdinalIgnoreCase)
            .Select(reviewer => Verdict([.. reviewer.OrderBy(review => review.SubmittedAt)]))
            .ToArray();
    }

    /// <summary>
    /// One reviewer's standing verdict: their latest decisive review, or their latest review of
    /// any kind when they have only ever commented.
    /// </summary>
    private static ForgeReview Verdict(IReadOnlyList<ForgeReview> history)
        => history.LastOrDefault(review => !IsComment(review)) ?? history[^1];

    private static bool IsComment(ForgeReview review)
        => string.Equals(review.State, "COMMENTED", StringComparison.OrdinalIgnoreCase);

    private static ForgeReview? ReadReview(JsonElement review)
    {
        var login = review.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object
            ? user.TryGetProperty("login", out var name) ? name.GetString() : null
            : null;
        if (string.IsNullOrWhiteSpace(login)) return null;
        var state = review.TryGetProperty("state", out var verdict) ? verdict.GetString() ?? "" : "";
        var submitted = review.TryGetProperty("submitted_at", out var at) && at.ValueKind == JsonValueKind.String
            ? at.GetDateTimeOffset()
            : DateTimeOffset.MinValue;
        var commit = review.TryGetProperty("commit_id", out var sha) && sha.ValueKind == JsonValueKind.String ? sha.GetString() : null;
        return new ForgeReview(login, state, submitted, commit);
    }

    /// <summary>
    /// Who the credential acts as. The workspace has an account whether or not a proposal is open,
    /// so this is read before the proposal and carried on every snapshot. A token the host will not
    /// identify leaves the account null; it is not worth failing an otherwise good read over.
    /// </summary>
    private static async Task<ForgeAccount?> ReadAccountAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            var user = await client.GetFromJsonAsync<JsonElement>("user", Json, cancellationToken);
            if (user.ValueKind != JsonValueKind.Object) return null;
            var login = user.TryGetProperty("login", out var name) ? name.GetString() : null;
            if (string.IsNullOrWhiteSpace(login)) return null;
            var display = user.TryGetProperty("name", out var full) ? full.GetString() : null;
            var type = user.TryGetProperty("type", out var kind) ? kind.GetString() : null;
            var url = user.TryGetProperty("html_url", out var link) ? link.GetString() : null;
            return new ForgeAccount(
                login,
                string.IsNullOrWhiteSpace(display) ? login : display,
                string.Equals(type, "Bot", StringComparison.OrdinalIgnoreCase),
                url ?? $"https://github.com/{login}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ForgeIssue>> ListAssignedAsync(
        ForgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        var issues = await client.GetFromJsonAsync<JsonElement>(
            "issues?filter=assigned&state=open&per_page=100",
            Json,
            cancellationToken);
        return Issues(issues);
    }

    public async Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
        ForgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        var issues = await client.GetFromJsonAsync<JsonElement>(
            $"repos/{request.Owner}/{request.Repository}/issues?state=open&sort=updated&direction=desc&per_page=100",
            Json,
            cancellationToken);
        return Issues(issues);
    }

    private static IReadOnlyList<ForgeIssue> Issues(JsonElement issues)
    {
        if (issues.ValueKind != JsonValueKind.Array)
            return [];

        return issues.EnumerateArray()
            // GitHub serves pull requests from the issues route as well, and a proposal is not a
            // task to pick up. Only an entry without the pull_request member is a real issue.
            .Where(issue => !issue.TryGetProperty("pull_request", out _))
            .Select(issue => new ForgeIssue(
                issue.GetProperty("id").GetRawText(),
                issue.GetProperty("number").GetInt32(),
                issue.GetProperty("title").GetString() ?? "",
                issue.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                issue.GetProperty("created_at").GetDateTimeOffset(),
                issue.GetProperty("updated_at").GetDateTimeOffset(),
                issue.GetProperty("html_url").GetString() ?? "",
                FirstAssigneeLogin(issue)))
            .ToArray();
    }

    /// <summary>The one assignee the Tasks page draws. GitHub can name several; we take the first.</summary>
    private static string? FirstAssigneeLogin(JsonElement issue)
    {
        if (issue.TryGetProperty("assignee", out var assignee)
            && assignee.ValueKind == JsonValueKind.Object
            && assignee.TryGetProperty("login", out var named)
            && named.GetString() is { Length: > 0 } login)
        {
            return login;
        }

        if (!issue.TryGetProperty("assignees", out var assignees) || assignees.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var user in assignees.EnumerateArray())
        {
            if (user.TryGetProperty("login", out var name) && name.GetString() is { Length: > 0 } listed)
                return listed;
        }

        return null;
    }

    public async Task<ForgeProposal> OpenProposalAsync(
        ForgeRequest request,
        ForgeOpenProposalCommand command,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        using var response = await client.PostAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/pulls",
            new { title = command.Title, body = command.Body, head = command.HeadRef, @base = command.BaseRef },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var pull = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        return ReadProposal(pull);
    }

    public async Task UpdateRefAsync(
        ForgeRequest request,
        ForgeUpdateRefCommand command,
        CancellationToken cancellationToken)
    {
        if (IsProtected(command.Ref))
            throw new ForgeProtectedRefException(command.Ref);

        using var client = CreateClient(request.Token, request.EndpointUrl);
        using var response = await client.PatchAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/git/refs/heads/{TrimHeads(command.Ref)}",
            new { sha = command.Sha, force = false },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CommentAsync(
        ForgeRequest request,
        ForgeCommentCommand command,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        using var response = await client.PostAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/issues/{command.ProposalNumber}/comments",
            new { body = command.Body },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SubmitReviewAsync(
        ForgeRequest request,
        ForgeSubmitReviewCommand command,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        // A finding sits on a line of the new side of the change.
        var comments = (command.Comments ?? [])
            .Select(comment => new { path = comment.Path, line = comment.Line, side = "RIGHT", body = comment.Body })
            .ToArray();
        var review = new System.Text.Json.Nodes.JsonObject { ["body"] = command.Body, ["event"] = command.Event };
        if (comments.Length > 0)
            review["comments"] = JsonSerializer.SerializeToNode(comments);
        if (!string.IsNullOrWhiteSpace(command.CommitId))
            review["commit_id"] = command.CommitId;
        using var response = await client.PostAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/pulls/{command.ProposalNumber}/reviews",
            review,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignIssueAsync(ForgeRequest request, int issueNumber, string login, CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        using var response = await client.PostAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/issues/{issueNumber}/assignees",
            new { assignees = new[] { login } },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RequestReviewAsync(ForgeRequest request, string proposalNumber, string login, CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        using var response = await client.PostAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/pulls/{proposalNumber}/requested_reviewers",
            new { reviewers = new[] { login } },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public static bool IsProtected(string @ref)
    {
        var name = TrimHeads(@ref);
        return name.Equals("main", StringComparison.OrdinalIgnoreCase)
            || name.Equals("master", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<ForgeCheck>> ReadChecksAsync(
        HttpClient client,
        ForgeRequest request,
        string sha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sha))
            return [];

        var document = await client.GetFromJsonAsync<JsonElement>(
            $"repos/{request.Owner}/{request.Repository}/commits/{sha}/check-runs",
            Json,
            cancellationToken);
        if (!document.TryGetProperty("check_runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
            return [];

        return runs.EnumerateArray()
            .Select(run => new ForgeCheck(
                run.GetProperty("name").GetString() ?? "",
                run.GetProperty("status").GetString() ?? "",
                run.TryGetProperty("conclusion", out var conclusion) ? conclusion.GetString() ?? "" : "",
                run.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : ""))
            .ToArray();
    }

    private async Task<IReadOnlyList<ForgeReviewThread>> ReadThreadsAsync(
        HttpClient client,
        ForgeRequest request,
        string number,
        CancellationToken cancellationToken)
    {
        var comments = await client.GetFromJsonAsync<JsonElement>(
            $"repos/{request.Owner}/{request.Repository}/pulls/{number}/comments",
            Json,
            cancellationToken);
        if (comments.ValueKind != JsonValueKind.Array)
            return [];

        return comments.EnumerateArray()
            .Select(comment => new ForgeReviewThread(
                comment.GetProperty("id").GetRawText(),
                comment.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "",
                comment.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                false,
                comment.TryGetProperty("path", out var path) ? path.GetString() : null,
                comment.TryGetProperty("line", out var line) && line.ValueKind == JsonValueKind.Number ? line.GetInt32() : null,
                comment.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object
                    && user.TryGetProperty("login", out var login) ? login.GetString() : null))
            .ToArray();
    }

    private static ForgeProposal ReadProposal(JsonElement pull)
        => new(
            pull.GetProperty("id").GetRawText(),
            pull.GetProperty("number").GetRawText(),
            pull.GetProperty("title").GetString() ?? "",
            pull.GetProperty("html_url").GetString() ?? "",
            pull.GetProperty("head").GetProperty("ref").GetString() ?? "",
            pull.GetProperty("base").GetProperty("ref").GetString() ?? "",
            pull.GetProperty("state").GetString() ?? "",
            pull.GetProperty("head").TryGetProperty("sha", out var head) ? head.GetString() : null);

    private static string TrimHeads(string @ref)
        => @ref.StartsWith("refs/heads/", StringComparison.Ordinal) ? @ref["refs/heads/".Length..] : @ref;

    public async Task<IReadOnlyList<ForgeRepository>> ListRepositoriesAsync(
        string token,
        string endpointUrl,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(token, endpointUrl);
        var repos = await client.GetFromJsonAsync<JsonElement>(
            "user/repos?per_page=100&sort=updated&affiliation=owner,collaborator,organization_member",
            Json,
            cancellationToken);
        return Repositories(repos);
    }

    public async Task<IReadOnlyList<ForgePullRequest>> ListPullRequestsAsync(
        ForgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        var pulls = await client.GetFromJsonAsync<JsonElement>(
            $"repos/{request.Owner}/{request.Repository}/pulls?state=open&sort=updated&direction=desc&per_page=20",
            Json,
            cancellationToken);
        if (pulls.ValueKind != JsonValueKind.Array)
            return [];

        var listed = new List<ForgePullRequest>();
        foreach (var pull in pulls.EnumerateArray())
        {
            var number = pull.GetProperty("number").GetRawText();
            IReadOnlyList<ForgeReview> reviews;
            try
            {
                reviews = await ReadReviewsAsync(client, request, number, cancellationToken);
            }
            catch (HttpRequestException)
            {
                reviews = [];
            }

            listed.Add(new ForgePullRequest(
                pull.GetProperty("id").GetRawText(),
                pull.GetProperty("number").GetInt32(),
                pull.GetProperty("title").GetString() ?? "",
                pull.GetProperty("html_url").GetString() ?? "",
                Logins(pull, "requested_reviewers"),
                reviews));
        }

        return listed;
    }

    public async Task<ForgeTokenAccess> ReadTokenAccessAsync(
        string token,
        string endpointUrl,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(token, endpointUrl);
        using var user = await client.GetAsync("user", cancellationToken);
        if (user.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new ForgeTokenAccess(false, false, "The token was refused.");
        user.EnsureSuccessStatusCode();

        if (user.Headers.TryGetValues("X-OAuth-Scopes", out var scopes)
            && !scopes.SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Any(scope => scope is "repo" or "public_repo"))
        {
            return new ForgeTokenAccess(
                true,
                false,
                "The token needs the repo scope so pull requests can be read. Dev review and PM review are inferred from them.");
        }

        using var repos = await client.GetAsync(
            "user/repos?per_page=1&affiliation=owner,collaborator,organization_member",
            cancellationToken);
        if (repos.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new ForgeTokenAccess(false, false, "The token cannot list repositories.");
        repos.EnsureSuccessStatusCode();
        var listed = await repos.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        if (listed.ValueKind != JsonValueKind.Array || listed.GetArrayLength() == 0)
            return new ForgeTokenAccess(true, true, null);

        var first = listed[0];
        var owner = first.GetProperty("owner").GetProperty("login").GetString();
        var name = first.GetProperty("name").GetString();
        using var pulls = await client.GetAsync($"repos/{owner}/{name}/pulls?state=open&per_page=1", cancellationToken);
        if (pulls.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new ForgeTokenAccess(
                true,
                false,
                "The token cannot read pull requests. Dev review and PM review are inferred from them.");
        }

        pulls.EnsureSuccessStatusCode();
        return new ForgeTokenAccess(true, true, null);
    }

    private static IReadOnlyList<ForgeRepository> Repositories(JsonElement repos)
    {
        if (repos.ValueKind != JsonValueKind.Array)
            return [];

        return repos.EnumerateArray()
            .Select(repo => new ForgeRepository(
                repo.GetProperty("owner").GetProperty("login").GetString() ?? "",
                repo.GetProperty("name").GetString() ?? ""))
            .Where(repo => repo.Owner.Length > 0 && repo.Name.Length > 0)
            .ToArray();
    }

    private static IReadOnlyList<string> Logins(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var users) || users.ValueKind != JsonValueKind.Array)
            return [];

        return users.EnumerateArray()
            .Select(user => user.TryGetProperty("login", out var login) ? login.GetString() : null)
            .OfType<string>()
            .Where(login => login.Length > 0)
            .ToArray();
    }

    private HttpClient CreateClient(string token, string? endpointUrl = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.BaseAddress = new Uri(ApiBaseFor(endpointUrl));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("agent-up");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private string ApiBaseFor(string? endpointUrl) =>
        string.IsNullOrWhiteSpace(endpointUrl)
            ? _apiBaseUrl
            : GithubHostConfigurationProvider.ApiBaseFromEndpoint(endpointUrl);
}
