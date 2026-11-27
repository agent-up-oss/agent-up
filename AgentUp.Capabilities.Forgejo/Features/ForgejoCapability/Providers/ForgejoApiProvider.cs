using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;

/// <summary>
/// Talks to the Forgejo REST API at <c>{origin}/api/v1/</c>. The handler is injectable so
/// tests can replay recorded responses without a live instance.
/// </summary>
public sealed class ForgejoApiProvider(HttpMessageHandler? handler = null, string? apiBaseUrl = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const int PullPageSize = 50;
    private const int PullPageLimit = 10;
    private readonly string _apiBaseUrl = ForgejoHostConfigurationProvider.ApiBase(apiBaseUrl);

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
        var history = await ReadJsonAsync(
            client,
            $"repos/{request.Owner}/{request.Repository}/pulls/{proposal.Number}/reviews?limit=100",
            cancellationToken);
        var threads = await ReadThreadsAsync(client, request, proposal.Number, history, cancellationToken);
        var reviews = Reviews(history);
        var mergeability = new ForgeMergeability(
            pull.TryGetProperty("mergeable", out var mergeable) && mergeable.ValueKind == JsonValueKind.True,
            pull.TryGetProperty("mergeable_state", out var state) ? state.GetString() ?? "unknown" : "unknown");
        return new ForgeHostSnapshot(proposal, checks, threads, mergeability, account, reviews);
    }

    public async Task<IReadOnlyList<ForgeIssue>> ListAssignedAsync(
        ForgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        var login = await ReadLoginAsync(client, cancellationToken);
        if (string.IsNullOrWhiteSpace(login))
            return [];

        var issues = await client.GetFromJsonAsync<JsonElement>(
            $"repos/{request.Owner}/{request.Repository}/issues?state=open&assigned=true&type=issues&limit=100",
            Json,
            cancellationToken);
        return Issues(issues, login);
    }

    public async Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
        ForgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        var issues = await client.GetFromJsonAsync<JsonElement>(
            $"repos/{request.Owner}/{request.Repository}/issues?state=open&type=issues&sort=recentupdate&limit=100",
            Json,
            cancellationToken);
        return Issues(issues);
    }

    // type=issues already keeps pull requests out, so both queries map the same payload.
    // assigned=true is advisory: hosts that ignore it still return the full list, so the
    // token owner's login is applied here rather than trusted from the query string.
    private static IReadOnlyList<ForgeIssue> Issues(JsonElement issues, string? assignedTo = null)
    {
        if (issues.ValueKind != JsonValueKind.Array)
            return [];

        return issues.EnumerateArray()
            .Where(issue => assignedTo is null || HasAssignee(issue, assignedTo))
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

    /// <summary>The one assignee the Tasks page draws. Forgejo can name several; we take the first.</summary>
    private static string? FirstAssigneeLogin(JsonElement issue)
    {
        if (!issue.TryGetProperty("assignees", out var assignees) || assignees.ValueKind != JsonValueKind.Array)
            return null;
        return assignees.EnumerateArray()
            .Select(user => user.TryGetProperty("login", out var name) ? name.GetString() : null)
            .FirstOrDefault(login => login is { Length: > 0 });
    }

    private static bool HasAssignee(JsonElement issue, string login)
    {
        if (!issue.TryGetProperty("assignees", out var assignees) || assignees.ValueKind != JsonValueKind.Array)
            return false;

        return assignees.EnumerateArray().Any(user =>
            user.TryGetProperty("login", out var name)
            && string.Equals(name.GetString(), login, StringComparison.OrdinalIgnoreCase));
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
        var name = TrimHeads(command.Ref);
        using var patch = await client.PatchAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/git/refs/heads/{name}",
            new { sha = command.Sha, force = false },
            cancellationToken);
        if (patch.IsSuccessStatusCode)
            return;
        if (patch.StatusCode != HttpStatusCode.MethodNotAllowed)
            patch.EnsureSuccessStatusCode();

        await RecreateBranchAsync(client, request, name, command.Sha, cancellationToken);
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
        // A finding sits on a line of the new side of the change, which Forgejo calls new_position.
        var comments = (command.Comments ?? [])
            .Select(comment => new { path = comment.Path, body = comment.Body, new_position = comment.Line })
            .ToArray();
        // Forgejo moves a pull's head after a push asynchronously; naming the reviewed commit keeps
        // a review that lands in between on the revision it is about.
        var review = new System.Text.Json.Nodes.JsonObject
        {
            ["body"] = command.Body,
            ["event"] = ReviewEvent(command.Event),
            ["comments"] = JsonSerializer.SerializeToNode(comments)
        };
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
        using var response = await client.PatchAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/issues/{issueNumber}",
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

    public async Task<ForgeAttachment?> AttachAsync(
        ForgeRequest request,
        string issueNumber,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        // An asset on the issue itself lists under its first post, which is the author's. The file
        // belongs to whoever shared it, so it goes on a comment of theirs that shows it.
        using var comment = await client.PostAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/issues/{issueNumber}/comments",
            new { body = $"Screenshot for my review: {fileName}" },
            Json,
            cancellationToken);
        comment.EnsureSuccessStatusCode();
        var commentId = (await comment.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken)).GetProperty("id").GetRawText();
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "attachment", fileName);
        using var response = await client.PostAsync(
            $"repos/{request.Owner}/{request.Repository}/issues/comments/{commentId}/assets?name={Uri.EscapeDataString(fileName)}",
            form,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var asset = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        var url = asset.TryGetProperty("browser_download_url", out var link) ? link.GetString() : null;
        return string.IsNullOrWhiteSpace(url) ? null : new ForgeAttachment(fileName, url);
    }

    /// <summary>
    /// Forgejo names an approval <c>APPROVED</c>; the Server's neutral name is <c>APPROVE</c>, the
    /// same as GitHub's. Other events pass through.
    /// </summary>
    public static string ReviewEvent(string @event) =>
        string.Equals(@event, "APPROVE", StringComparison.OrdinalIgnoreCase) ? "APPROVED" : @event.ToUpperInvariant();

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
            $"repos/{request.Owner}/{request.Repository}/commits/{sha}/statuses",
            Json,
            cancellationToken);
        if (document.ValueKind != JsonValueKind.Array)
            return [];

        return document.EnumerateArray()
            .Select(status =>
            {
                var state = status.TryGetProperty("status", out var statusValue)
                    ? statusValue.GetString() ?? ""
                    : status.TryGetProperty("state", out var stateValue) ? stateValue.GetString() ?? "" : "";
                var pending = state.Equals("pending", StringComparison.OrdinalIgnoreCase);
                return new ForgeCheck(
                    status.TryGetProperty("context", out var context) ? context.GetString() ?? "" : "",
                    pending ? "in_progress" : "completed",
                    pending ? "" : state,
                    status.TryGetProperty("target_url", out var url) ? url.GetString() ?? "" : "");
            })
            .ToArray();
    }

    /// <summary>
    /// Findings on lines of the change belong to the review that raised them, so each review's own
    /// comments are read; the conversation is the issue comments. Forgejo has no flat list of a
    /// pull's line comments.
    /// </summary>
    private async Task<IReadOnlyList<ForgeReviewThread>> ReadThreadsAsync(
        HttpClient client,
        ForgeRequest request,
        string number,
        JsonElement history,
        CancellationToken cancellationToken)
    {
        var threads = new List<ForgeReviewThread>();
        var withFindings = (history.ValueKind == JsonValueKind.Array ? history.EnumerateArray() : Enumerable.Empty<JsonElement>())
            .Where(HasFindings);
        foreach (var review in withFindings)
        {
            var inline = await ReadJsonAsync(
                client,
                $"repos/{request.Owner}/{request.Repository}/pulls/{number}/reviews/{review.GetProperty("id").GetRawText()}/comments",
                cancellationToken);
            threads.AddRange(Threads(inline));
        }

        var conversation = await ReadJsonAsync(
            client,
            $"repos/{request.Owner}/{request.Repository}/issues/{number}/comments",
            cancellationToken);
        threads.AddRange(Threads(conversation));
        return threads;
    }

    private static bool HasFindings(JsonElement review)
        => review.TryGetProperty("comments_count", out var count) && count.ValueKind == JsonValueKind.Number && count.GetInt32() > 0;

    private static IEnumerable<ForgeReviewThread> Threads(JsonElement comments)
    {
        if (comments.ValueKind != JsonValueKind.Array)
            return [];

        return comments.EnumerateArray()
            .Select(comment => new ForgeReviewThread(
                comment.GetProperty("id").GetRawText(),
                comment.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "",
                comment.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                false,
                comment.TryGetProperty("path", out var path) ? path.GetString() : null,
                Line(comment),
                comment.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object ? ReadLogin(user) : null));
    }

    /// <summary>The line on the new side; a comment left on the old side falls back to that one.</summary>
    private static int? Line(JsonElement comment)
    {
        var line = new[] { "position", "original_position" }
            .Select(name => comment.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0)
            .FirstOrDefault(value => value > 0);
        return line > 0 ? line : null;
    }

    /// <summary>
    /// Each reviewer's standing verdict. Requesting a reviewer adds an entry with no verdict of its
    /// own, so only approvals, change requests and comments count.
    /// </summary>
    private static IReadOnlyList<ForgeReview> Reviews(JsonElement reviews)
    {
        if (reviews.ValueKind != JsonValueKind.Array)
            return [];

        return reviews.EnumerateArray()
            .Select(ReadReview)
            .OfType<ForgeReview>()
            .Where(review => !review.State.Equals("REQUEST_REVIEW", StringComparison.OrdinalIgnoreCase)
                && !review.State.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
            .GroupBy(review => review.Login, StringComparer.OrdinalIgnoreCase)
            .Select(reviewer => Verdict([.. reviewer.OrderBy(review => review.SubmittedAt)]))
            .ToArray();
    }

    private static ForgeReview Verdict(IReadOnlyList<ForgeReview> history)
        => history.LastOrDefault(review => !IsComment(review)) ?? history[^1];

    private static bool IsComment(ForgeReview review)
        => review.State.Equals("COMMENTED", StringComparison.OrdinalIgnoreCase)
           || review.State.Equals("COMMENT", StringComparison.OrdinalIgnoreCase);

    private static ForgeReview? ReadReview(JsonElement review)
    {
        var login = review.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object
            ? user.TryGetProperty("login", out var name) ? name.GetString() : null
            : null;
        if (string.IsNullOrWhiteSpace(login))
            return null;
        var state = review.TryGetProperty("state", out var verdict) ? verdict.GetString() ?? "" : "";
        var submitted = review.TryGetProperty("submitted_at", out var at) && at.ValueKind == JsonValueKind.String
            ? at.GetDateTimeOffset()
            : DateTimeOffset.MinValue;
        var commit = review.TryGetProperty("commit_id", out var sha) && sha.ValueKind == JsonValueKind.String ? sha.GetString() : null;
        return new ForgeReview(login, state, submitted, commit);
    }

    private static async Task<ForgeAccount?> ReadAccountAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var user = await ReadJsonAsync(client, "user", cancellationToken);
        var login = ReadLogin(user);
        if (string.IsNullOrWhiteSpace(login))
            return null;

        var display = user.TryGetProperty("full_name", out var full) ? full.GetString() : null;
        if (string.IsNullOrWhiteSpace(display))
            display = user.TryGetProperty("name", out var name) ? name.GetString() : null;
        var url = user.TryGetProperty("html_url", out var link) ? link.GetString() : null;
        return new ForgeAccount(
            login,
            string.IsNullOrWhiteSpace(display) ? login : display,
            false,
            url ?? "");
    }

    private static async Task<string?> ReadLoginAsync(HttpClient client, CancellationToken cancellationToken)
        => ReadLogin(await ReadJsonAsync(client, "user", cancellationToken));

    private static string? ReadLogin(JsonElement user)
        => user.ValueKind == JsonValueKind.Object && user.TryGetProperty("login", out var name)
            ? name.GetString()
            : null;

    private static async Task RecreateBranchAsync(
        HttpClient client,
        ForgeRequest request,
        string name,
        string sha,
        CancellationToken cancellationToken)
    {
        using var delete = await client.DeleteAsync(
            $"repos/{request.Owner}/{request.Repository}/branches/{name}",
            cancellationToken);
        if (!delete.IsSuccessStatusCode && delete.StatusCode != HttpStatusCode.NotFound)
            delete.EnsureSuccessStatusCode();

        using var create = await client.PostAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/branches",
            new { new_branch_name = name, old_ref_name = sha },
            cancellationToken);
        create.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> ReadJsonAsync(
        HttpClient client,
        string uri,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return default;

        return await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
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
        var repos = await client.GetFromJsonAsync<JsonElement>("user/repos?limit=100", Json, cancellationToken);
        return Repositories(repos);
    }

    public async Task<IReadOnlyList<ForgePullRequest>> ListPullRequestsAsync(
        ForgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token, request.EndpointUrl);
        var listed = new List<ForgePullRequest>();
        for (var page = 1; page <= PullPageLimit; page++)
        {
            var pulls = await client.GetFromJsonAsync<JsonElement>(
                $"repos/{request.Owner}/{request.Repository}/pulls?state=open&limit={PullPageSize}&page={page}",
                Json,
                cancellationToken);
            if (pulls.ValueKind != JsonValueKind.Array || pulls.GetArrayLength() == 0)
                break;
            foreach (var pull in pulls.EnumerateArray())
            {
                var item = await ReadListedPullAsync(client, request, pull, cancellationToken);
                listed.Add(item);
            }
            if (pulls.GetArrayLength() < PullPageSize)
                break;
        }

        return listed;
    }

    private static async Task<ForgePullRequest> ReadListedPullAsync(
        HttpClient client,
        ForgeRequest request,
        JsonElement pull,
        CancellationToken cancellationToken)
    {
        var number = pull.GetProperty("number").GetRawText();
        IReadOnlyList<ForgeReview> reviews;
        try
        {
            var body = await client.GetFromJsonAsync<JsonElement>(
                $"repos/{request.Owner}/{request.Repository}/pulls/{number}/reviews?limit=100",
                Json,
                cancellationToken);
            reviews = body.ValueKind == JsonValueKind.Array
                ? body.EnumerateArray().Select(ReadOneReview).OfType<ForgeReview>().ToArray()
                : [];
        }
        catch (HttpRequestException)
        {
            reviews = [];
        }

        return new ForgePullRequest(
            pull.GetProperty("id").GetRawText(),
            pull.GetProperty("number").GetInt32(),
            pull.GetProperty("title").GetString() ?? "",
            pull.GetProperty("html_url").GetString() ?? "",
            Logins(pull, "requested_reviewers"),
            reviews,
            Nested(pull, "head", "ref"),
            Nested(pull, "base", "ref"),
            Nested(pull, "head", "sha"),
            pull.TryGetProperty("body", out var description) ? description.GetString() : null,
            NestedOwner(pull, "head"));
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

        using var repos = await client.GetAsync("user/repos?limit=1", cancellationToken);
        if (repos.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new ForgeTokenAccess(false, false, "The token cannot list repositories.");
        repos.EnsureSuccessStatusCode();
        var listed = await repos.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        if (listed.ValueKind != JsonValueKind.Array || listed.GetArrayLength() == 0)
            return new ForgeTokenAccess(true, true, null);

        var first = listed[0];
        var owner = OwnerOf(first);
        var name = first.GetProperty("name").GetString();
        using var pulls = await client.GetAsync($"repos/{owner}/{name}/pulls?state=open&limit=1", cancellationToken);
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
            .Select(repo => new ForgeRepository(OwnerOf(repo), repo.GetProperty("name").GetString() ?? ""))
            .Where(repo => repo.Owner.Length > 0 && repo.Name.Length > 0)
            .ToArray();
    }

    private static string OwnerOf(JsonElement repo)
    {
        if (!repo.TryGetProperty("owner", out var owner) || owner.ValueKind != JsonValueKind.Object)
            return "";
        if (owner.TryGetProperty("login", out var login) && login.GetString() is { Length: > 0 } named)
            return named;
        return owner.TryGetProperty("username", out var username) ? username.GetString() ?? "" : "";
    }

    private static ForgeReview? ReadOneReview(JsonElement review)
    {
        var login = review.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object
            ? user.TryGetProperty("login", out var name) ? name.GetString() : null
            : null;
        if (string.IsNullOrWhiteSpace(login))
            return null;
        var state = review.TryGetProperty("state", out var verdict) ? verdict.GetString() ?? "" : "";
        var submitted = review.TryGetProperty("submitted_at", out var at) && at.ValueKind == JsonValueKind.String
            ? at.GetDateTimeOffset()
            : DateTimeOffset.MinValue;
        var commit = review.TryGetProperty("commit_id", out var sha) && sha.ValueKind == JsonValueKind.String ? sha.GetString() : null;
        return new ForgeReview(login, state, submitted, commit);
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

    private static string? Nested(JsonElement pull, string side, string field) =>
        pull.TryGetProperty(side, out var node)
        && node.ValueKind == JsonValueKind.Object
        && node.TryGetProperty(field, out var value)
            ? value.GetString()
            : null;

    private static string? NestedOwner(JsonElement pull, string side)
    {
        if (!pull.TryGetProperty(side, out var node) || node.ValueKind != JsonValueKind.Object)
            return null;
        if (!node.TryGetProperty("repo", out var repo) || repo.ValueKind != JsonValueKind.Object)
            return null;
        if (!repo.TryGetProperty("owner", out var owner) || owner.ValueKind != JsonValueKind.Object)
            return null;
        return owner.TryGetProperty("login", out var login) ? login.GetString() : null;
    }

    private HttpClient CreateClient(string token, string? endpointUrl = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.BaseAddress = new Uri(ApiBaseFor(endpointUrl));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("agent-up");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private string ApiBaseFor(string? endpointUrl) =>
        string.IsNullOrWhiteSpace(endpointUrl)
            ? _apiBaseUrl
            : ForgejoHostConfigurationProvider.ApiBaseFromEndpoint(endpointUrl);
}
