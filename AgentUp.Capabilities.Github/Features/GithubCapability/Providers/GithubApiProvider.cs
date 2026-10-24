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
        return new ForgeReview(login, state, submitted);
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

    public async Task<IReadOnlyList<ForgeAssignedIssue>> ListAssignedAsync(
        ForgeRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(request.Token);
        var issues = await client.GetFromJsonAsync<JsonElement>(
            "issues?filter=assigned&state=open&per_page=100",
            Json,
            cancellationToken);
        if (issues.ValueKind != JsonValueKind.Array)
            return [];

        return issues.EnumerateArray()
            .Select(issue => new ForgeAssignedIssue(
                issue.GetProperty("id").GetRawText(),
                issue.GetProperty("number").GetInt32(),
                issue.GetProperty("title").GetString() ?? "",
                issue.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                issue.GetProperty("created_at").GetDateTimeOffset(),
                issue.GetProperty("updated_at").GetDateTimeOffset(),
                issue.GetProperty("html_url").GetString() ?? ""))
            .ToArray();
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
                false))
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
            pull.GetProperty("state").GetString() ?? "");

    private static string TrimHeads(string @ref)
        => @ref.StartsWith("refs/heads/", StringComparison.Ordinal) ? @ref["refs/heads/".Length..] : @ref;

    private HttpClient CreateClient(string token)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.BaseAddress = new Uri(_apiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("agent-up");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
