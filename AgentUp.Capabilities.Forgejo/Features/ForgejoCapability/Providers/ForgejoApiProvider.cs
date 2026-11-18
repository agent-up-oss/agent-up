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
    private readonly string _apiBaseUrl = ForgejoHostConfigurationProvider.ApiBase(apiBaseUrl);

    public async Task<ForgeHostSnapshot> ReadAsync(ForgeRequest request, CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token);
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

    public async Task<IReadOnlyList<ForgeIssue>> ListAssignedAsync(
        ForgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token);
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
        using var client = CreateClient(request.Token);
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
                issue.GetProperty("html_url").GetString() ?? ""))
            .ToArray();
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
        using var client = CreateClient(request.Token);
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

        using var client = CreateClient(request.Token);
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
        using var client = CreateClient(request.Token);
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
        using var client = CreateClient(request.Token);
        using var response = await client.PostAsJsonAsync(
            $"repos/{request.Owner}/{request.Repository}/pulls/{command.ProposalNumber}/reviews",
            new { body = command.Body, @event = command.Event },
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

    private async Task<IReadOnlyList<ForgeReviewThread>> ReadThreadsAsync(
        HttpClient client,
        ForgeRequest request,
        string number,
        CancellationToken cancellationToken)
    {
        var inline = await ReadJsonAsync(
            client,
            $"repos/{request.Owner}/{request.Repository}/pulls/{number}/comments",
            cancellationToken);
        var conversation = await ReadJsonAsync(
            client,
            $"repos/{request.Owner}/{request.Repository}/issues/{number}/comments",
            cancellationToken);
        return Threads(inline).Concat(Threads(conversation)).ToArray();
    }

    private static IEnumerable<ForgeReviewThread> Threads(JsonElement comments)
    {
        if (comments.ValueKind != JsonValueKind.Array)
            return [];

        return comments.EnumerateArray()
            .Select(comment => new ForgeReviewThread(
                comment.GetProperty("id").GetRawText(),
                comment.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "",
                comment.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                false));
    }

    private async Task<IReadOnlyList<ForgeReview>> ReadReviewsAsync(
        HttpClient client,
        ForgeRequest request,
        string proposalNumber,
        CancellationToken cancellationToken)
    {
        var reviews = await ReadJsonAsync(
            client,
            $"repos/{request.Owner}/{request.Repository}/pulls/{proposalNumber}/reviews?limit=100",
            cancellationToken);
        if (reviews.ValueKind != JsonValueKind.Array)
            return [];

        return reviews.EnumerateArray()
            .Select(ReadReview)
            .OfType<ForgeReview>()
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
        return new ForgeReview(login, state, submitted);
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
            pull.GetProperty("state").GetString() ?? "");

    private static string TrimHeads(string @ref)
        => @ref.StartsWith("refs/heads/", StringComparison.Ordinal) ? @ref["refs/heads/".Length..] : @ref;

    private HttpClient CreateClient(string token)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.BaseAddress = new Uri(_apiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("agent-up");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
