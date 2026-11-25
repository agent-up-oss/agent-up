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
            ("GET /user", User),
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", Statuses),
            ("GET /repos/acme/widgets/issues/42/comments", Comments));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Proposal!.Number, Is.EqualTo("42"));
            Assert.That(snapshot.Checks.Single().Name, Is.EqualTo("ci"));
            Assert.That(snapshot.Checks.Single().Conclusion, Is.EqualTo("success"));
            Assert.That(snapshot.Threads.Single().Id, Is.EqualTo("99"));
            Assert.That(snapshot.Mergeability!.Mergeable, Is.True);
            Assert.That(snapshot.Account!.Login, Is.EqualTo("agentup"));
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
                ("GET /user", User),
                ("GET /repos/acme/widgets/issues?state=open&assigned=true&type=issues&limit=100", Assigned)))
            .ListAssignedAsync(Request());

        Assert.That(issues.Single().Number, Is.EqualTo(3));
        Assert.That(issues.Single().Id, Is.EqualTo("7"));
    }

    [Test]
    public async Task ListAssignedAsync_returnsEmptyWhenThePayloadIsNotAnArray()
    {
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /user", User),
                ("GET /repos/acme/widgets/issues?state=open&assigned=true&type=issues&limit=100", "{}")))
            .ListAssignedAsync(Request());

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task ListAssignedAsync_dropsIssuesNotAssignedToTheTokenOwner()
    {
        const string mixed = """
            [{"id":7,"number":3,"title":"Assigned","body":"do it","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example.test/acme/widgets/issues/3","assignees":[{"login":"agentup"}]},
             {"id":8,"number":4,"title":"Other","body":"no","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example.test/acme/widgets/issues/4","assignees":[]}]
            """;
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /user", User),
                ("GET /repos/acme/widgets/issues?state=open&assigned=true&type=issues&limit=100", mixed)))
            .ListAssignedAsync(Request());

        Assert.That(issues.Single().Number, Is.EqualTo(3));
    }

    [Test]
    public async Task ListAssignedAsync_returnsEmptyWhenTheTokenOwnerCannotBeRead()
    {
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/issues?state=open&assigned=true&type=issues&limit=100", Assigned)))
            .ListAssignedAsync(Request());

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task ListAssignedAsync_dropsIssuesWithoutAnAssigneesList()
    {
        const string mixed = """
            [{"id":7,"number":3,"title":"Assigned","body":"do it","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example.test/acme/widgets/issues/3"},
             {"id":8,"number":4,"title":"Object","body":"no","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example.test/acme/widgets/issues/4","assignees":{}},
             {"id":9,"number":5,"title":"No login","body":"no","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example.test/acme/widgets/issues/5","assignees":[{"id":1}]}]
            """;
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /user", User),
                ("GET /repos/acme/widgets/issues?state=open&assigned=true&type=issues&limit=100", mixed)))
            .ListAssignedAsync(Request());

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task ListAssignedAsync_matchesAssigneeLoginWithoutRegardToCase()
    {
        const string mixed = """
            [{"id":7,"number":3,"title":"Assigned","body":"do it","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example.test/acme/widgets/issues/3","assignees":[{"login":"AgentUp"}]}]
            """;
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /user", User),
                ("GET /repos/acme/widgets/issues?state=open&assigned=true&type=issues&limit=100", mixed)))
            .ListAssignedAsync(Request());

        Assert.That(issues.Single().Number, Is.EqualTo(3));
    }

    [Test]
    public async Task ListIssuesAsync_readsTheRepositoryWithoutTheAssignedFilter()
    {
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/issues?state=open&type=issues&sort=recentupdate&limit=100", Assigned)))
            .ListIssuesAsync(Request());

        Assert.That(issues.Single().Number, Is.EqualTo(3));
        Assert.That(issues.Single().Id, Is.EqualTo("7"));
        Assert.That(issues.Single().AssigneeLogin, Is.EqualTo("agentup"));
    }

    [Test]
    public async Task ListIssuesAsync_readsMissingOrNullTextAsEmpty()
    {
        var issues = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/issues?state=open&type=issues&sort=recentupdate&limit=100", SparseIssues)))
            .ListIssuesAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(issues.Select(issue => issue.Title), Is.EqualTo(new[] { "", "Restock" }));
            Assert.That(issues.Select(issue => issue.Body), Is.EqualTo(new[] { "", "" }));
            Assert.That(issues.Select(issue => issue.Url), Is.EqualTo(new[] { "", "https://forgejo.example/acme/widgets/issues/9" }));
        });
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
    public async Task UpdateRefAsync_recreatesTheBranchWhenGitRefsPatchIsNotImplemented()
    {
        var handler = Recorded(
            ("PATCH /repos/acme/widgets/git/refs/heads/topic", HttpStatusCode.MethodNotAllowed, ""),
            ("DELETE /repos/acme/widgets/branches/topic", HttpStatusCode.NoContent, ""),
            ("POST /repos/acme/widgets/branches", HttpStatusCode.Created, "{}"));

        await new ForgejoForgeCapability(handler).UpdateRefAsync(Request(), new ForgeUpdateRefCommand("topic", "abc"));

        Assert.That(handler.Requests, Is.EqualTo(new[]
        {
            "PATCH /repos/acme/widgets/git/refs/heads/topic",
            "DELETE /repos/acme/widgets/branches/topic",
            "POST /repos/acme/widgets/branches"
        }));
    }

    [Test]
    public async Task UpdateRefAsync_recreatesTheBranchWhenTheOldBranchIsAlreadyGone()
    {
        var handler = Recorded(
            ("PATCH /repos/acme/widgets/git/refs/heads/topic", HttpStatusCode.MethodNotAllowed, ""),
            ("DELETE /repos/acme/widgets/branches/topic", HttpStatusCode.NotFound, ""),
            ("POST /repos/acme/widgets/branches", HttpStatusCode.Created, "{}"));

        await new ForgejoForgeCapability(handler).UpdateRefAsync(Request(), new ForgeUpdateRefCommand("topic", "abc"));

        Assert.That(handler.Requests, Does.Contain("POST /repos/acme/widgets/branches"));
    }

    [Test]
    public void UpdateRefAsync_throwsWhenGitRefsPatchFailsForAReasonOtherThanMethodNotAllowed()
    {
        var handler = Recorded(("PATCH /repos/acme/widgets/git/refs/heads/topic", HttpStatusCode.BadRequest, ""));

        Assert.ThrowsAsync<HttpRequestException>(() =>
            new ForgejoForgeCapability(handler).UpdateRefAsync(Request(), new ForgeUpdateRefCommand("topic", "abc")));
    }

    [Test]
    public void UpdateRefAsync_throwsWhenRecreatingTheBranchCannotDeleteTheOldRef()
    {
        var handler = Recorded(
            ("PATCH /repos/acme/widgets/git/refs/heads/topic", HttpStatusCode.MethodNotAllowed, ""),
            ("DELETE /repos/acme/widgets/branches/topic", HttpStatusCode.Conflict, ""));

        Assert.ThrowsAsync<HttpRequestException>(() =>
            new ForgejoForgeCapability(handler).UpdateRefAsync(Request(), new ForgeUpdateRefCommand("topic", "abc")));
    }

    // Forgejo keeps a finding with the review that raised it; there is no flat list of line comments.
    [Test]
    public async Task ReadAsync_readsEachReviewsFindingsWithTheirLineAndAuthor()
    {
        const string reviews = """
            [{"id":7,"user":{"login":"hector"},"state":"REQUEST_REVIEW","submitted_at":"2026-01-01T00:00:00Z","comments_count":0},
             {"id":8,"user":{"login":"hector"},"state":"REQUEST_CHANGES","submitted_at":"2026-01-02T00:00:00Z","commit_id":"abc123","comments_count":1}]
            """;
        const string findings = """
            [{"id":22,"path":"test/page.test.js","position":8,"original_position":0,"body":"Assert the new headline.","html_url":"https://forgejo.example.test/acme/widgets/pulls/42#issuecomment-22","user":{"login":"hector"}}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", reviews),
            ("GET /repos/acme/widgets/pulls/42/reviews/8/comments", findings),
            ("GET /repos/acme/widgets/issues/42/comments", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Threads.Single(), Is.EqualTo(new ForgeReviewThread(
                "22", "https://forgejo.example.test/acme/widgets/pulls/42#issuecomment-22", "Assert the new headline.", false, "test/page.test.js", 8, "hector")));
            Assert.That(snapshot.Reviews!.Single(), Is.EqualTo(new ForgeReview("hector", "REQUEST_CHANGES", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "abc123")),
                "asking for a review is not a verdict");
            Assert.That(snapshot.Proposal!.HeadSha, Is.EqualTo("abc123"));
            Assert.That(handler.Requests, Has.None.Contains("reviews/7/comments"), "a review with no findings is not read");
        });
    }

    [Test]
    public async Task SubmitReviewAsync_postsEachFindingOnItsLine()
    {
        var handler = Recorded(("POST /repos/acme/widgets/pulls/42/reviews", "{}"));

        await new ForgejoForgeCapability(handler).SubmitReviewAsync(Request(), new ForgeSubmitReviewCommand(
            "42", "One thing.", "REQUEST_CHANGES", [new ForgeReviewComment("src/greeting.js", 1, "Keep the comma.")], "abc123"));

        // The reviewed commit is named, so a review landing before Forgejo moves the pull's head
        // after a push is still filed against the revision it is about.
        Assert.That(handler.Bodies.Single(), Does.Contain("\"path\":\"src/greeting.js\"").And.Contain("\"new_position\":1").And.Contain("\"event\":\"REQUEST_CHANGES\"").And.Contain("\"commit_id\":\"abc123\""));
    }

    [Test]
    public async Task AssignIssueAsync_andRequestReviewAsync_nameTheTeamMembersAccount()
    {
        var handler = Recorded(
            ("PATCH /repos/acme/widgets/issues/3", "{}"),
            ("POST /repos/acme/widgets/pulls/42/requested_reviewers", "[]"));
        var forge = new ForgejoForgeCapability(handler);

        await forge.AssignIssueAsync(Request(), 3, "bob");
        await forge.RequestReviewAsync(Request(), "42", "hector");

        Assert.That(handler.Bodies, Is.EqualTo(new[] { """{"assignees":["bob"]}""", """{"reviewers":["hector"]}""" }));
    }

    [Test]
    // The file belongs to whoever shared it, so it hangs off a comment of theirs rather than the
    // proposal's first post, which is the author's.
    public async Task AttachAsync_uploadsTheFileOnACommentOfTheSharersAndReturnsWhereToOpenIt()
    {
        var handler = Recorded(
            ("POST /repos/acme/widgets/issues/42/comments", """{"id":7}"""),
            ("POST /repos/acme/widgets/issues/comments/7/assets?name=shot.png",
                """{"name":"shot.png","browser_download_url":"https://forgejo.example.test/attachments/abc"}"""));

        var attachment = await new ForgejoForgeCapability(handler).AttachAsync(Request(), "42", "shot.png", "image/png", [1, 2, 3]);

        Assert.Multiple(() =>
        {
            Assert.That(attachment, Is.EqualTo(new ForgeAttachment("shot.png", "https://forgejo.example.test/attachments/abc")));
            Assert.That(handler.Bodies[0], Does.Contain("Screenshot for my review: shot.png"));
            Assert.That(handler.Bodies[1], Does.Contain("name=attachment"));
        });
    }

    [Test]
    public async Task AttachAsync_returnsNothingWhenTheHostNamesNoLink()
    {
        var handler = Recorded(
            ("POST /repos/acme/widgets/issues/42/comments", """{"id":7}"""),
            ("POST /repos/acme/widgets/issues/comments/7/assets?name=shot.png", "{}"));

        var attachment = await new ForgejoForgeCapability(handler).AttachAsync(Request(), "42", "shot.png", "image/png", [1]);

        Assert.That(attachment, Is.Null);
    }

    [Test]
    public async Task ReadAsync_readsIssueConversationWhenPullCommentsAreNotImplemented()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/issues/42/comments", Comments));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Threads.Single().Id, Is.EqualTo("99"));
        Assert.That(snapshot.Threads.Single().Summary, Is.EqualTo("please fix"));
    }

    [Test]
    public async Task ReadAsync_mapsReviewsFromThePullReviewsRoute()
    {
        const string reviews = """
            [{"id":1,"user":{"login":"agentup"},"state":"COMMENT","submitted_at":"2026-01-01T00:00:00Z"}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"),
            ("GET /repos/acme/widgets/issues/42/comments", "[]"),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", reviews));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        var mapped = snapshot.Reviews ?? [];
        Assert.That(mapped, Has.Count.EqualTo(1));
        Assert.That(mapped[0].Login, Is.EqualTo("agentup"));
        Assert.That(mapped[0].State, Is.EqualTo("COMMENT"));
    }

    [Test]
    public async Task ReadAsync_keepsOnlyEachReviewersLatestVerdict()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"),
            ("GET /repos/acme/widgets/issues/42/comments", "[]"),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", Reviews));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Reviews!, Has.Count.EqualTo(2), "one verdict per reviewer, not one per review");
            Assert.That(snapshot.Reviews!.Single(review => review.Login == "hector").State,
                Is.EqualTo("APPROVED"), "the approval came after the changes were requested");
            Assert.That(snapshot.Reviews!.Single(review => review.Login == "lucy").State,
                Is.EqualTo("COMMENTED"));
        });
    }

    [Test]
    public async Task ReadAsync_aLaterCommentDoesNotUnseatADecisiveVerdict()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"),
            ("GET /repos/acme/widgets/issues/42/comments", "[]"),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", CommentedAfterVerdict));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Reviews!.Single(review => review.Login == "hector").State,
                Is.EqualTo("APPROVED"), "a comment after approving does not withdraw the approval");
            Assert.That(snapshot.Reviews!.Single(review => review.Login == "lucy").State,
                Is.EqualTo("REQUEST_CHANGES"), "a comment after requesting changes still blocks");
        });
    }

    [Test]
    public async Task ReadAsync_skipsAReviewWithNoReviewerAndToleratesMissingStateOrTime()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"),
            ("GET /repos/acme/widgets/issues/42/comments", "[]"),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", PartialReviews));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());
        var reviews = snapshot.Reviews!.OrderBy(review => review.Login).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(reviews.Select(review => review.Login), Is.EqualTo(new[] { "mia", "ola" }),
                "entries without a reviewer login are not verdicts");
            Assert.That(reviews.Select(review => review.State), Is.EqualTo(new[] { "", "" }));
            Assert.That(reviews.Select(review => review.SubmittedAt), Is.All.EqualTo(DateTimeOffset.MinValue));
        });
    }

    [Test]
    public async Task ReadAsync_readsNoVerdictsWhenTheReviewsPayloadIsNotAList()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"),
            ("GET /repos/acme/widgets/issues/42/comments", "[]"),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", """{"message":"Not Found"}"""));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Reviews!, Is.Empty);
    }

    [TestCase("[]")]
    [TestCase("""{"name":"No login"}""")]
    [TestCase("""{"login":"  "}""")]
    public async Task ReadAsync_leavesTheAccountUnsetWhenTheHostNamesNoLogin(string user)
    {
        var handler = Recorded(
            ("GET /user", user),
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Account, Is.Null);
    }

    [Test]
    public async Task ReadAsync_fallsBackToNameWhenFullNameIsBlank()
    {
        var handler = Recorded(
            ("GET /user", """{"login":"dana","name":"Dana","html_url":"https://forgejo.example.test/dana"}"""),
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.That(snapshot.Account!.DisplayName, Is.EqualTo("Dana"));
    }

    [Test]
    public async Task ReadAsync_usesTheLoginWhenTheDisplayNameIsBlank()
    {
        var handler = Recorded(
            ("GET /user", """{"login":"dana"}"""),
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Account!.DisplayName, Is.EqualTo("dana"));
            Assert.That(snapshot.Account.Url, Is.EqualTo(""));
        });
    }

    [Test]
    public async Task ReadAsync_treatsANullHeadShaAsMissing()
    {
        const string pulls = """
            [{"id":1,"number":42,"title":"Topic","html_url":"https://forgejo.example.test/acme/widgets/pulls/42","state":"open","mergeable":true,"mergeable_state":null,"head":{"ref":"topic","sha":null},"base":{"ref":"main"}}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", pulls),
            ("GET /repos/acme/widgets/pulls/42/comments", "[]"));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Checks, Is.Empty);
            Assert.That(snapshot.Mergeability!.State, Is.EqualTo("unknown"));
            Assert.That(handler.Requests, Has.None.Contains("statuses"));
        });
    }

    [Test]
    public async Task ReadAsync_readsMissingThreadTextAsEmpty()
    {
        const string comments = """
            [{"id":99,"html_url":null,"body":null},
             {"id":100}]
            """;
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", Pulls),
            ("GET /repos/acme/widgets/commits/abc123/statuses", "[]"),
            ("GET /repos/acme/widgets/issues/42/comments", comments));
        var snapshot = await new ForgejoForgeCapability(handler).ReadAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Threads.Select(thread => thread.Id), Is.EqualTo(new[] { "99", "100" }));
            Assert.That(snapshot.Threads.Select(thread => thread.Url), Is.All.EqualTo(""));
            Assert.That(snapshot.Threads.Select(thread => thread.Summary), Is.All.EqualTo(""));
        });
    }

    [Test]
    public async Task ReadAsync_returnsEmptyWhenThePullsPayloadIsNotAnArray()
    {
        var snapshot = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/pulls?head=acme:topic&state=open", "{}")))
            .ReadAsync(Request());

        Assert.That(snapshot.Proposal, Is.Null);
        Assert.That(snapshot.Checks, Is.Empty);
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

        Assert.That(handler.Uris, Has.All.StartsWith("https://forgejo.example.test/api/v1/"));
        Assert.That(handler.Uris, Has.Some.Contains("/repos/acme/widgets/pulls"));
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
    public void ApiBaseFromEndpoint_mapsAnOriginToApiV1()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ForgejoHostConfigurationProvider.ApiBaseFromEndpoint("https://git.example.test"), Is.EqualTo("https://git.example.test/api/v1/"));
            Assert.That(ForgejoHostConfigurationProvider.ApiBaseFromEndpoint("https://git.example.test/api/v1"), Is.EqualTo("https://git.example.test/api/v1/"));
        });
    }

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
    public async Task ListRepositoriesAsync_readsTheRepositoriesTheTokenCanSee()
    {
        var handler = Recorded(("GET /user/repos?limit=100", """
            [{"name":"widgets","owner":{"login":"acme"}}]
            """));

        var repositories = await new ForgejoForgeCapability(handler).ListRepositoriesAsync("token", "https://git.example.test");

        Assert.That(repositories.Select(repository => $"{repository.Owner}/{repository.Name}"), Is.EqualTo(new[] { "acme/widgets" }));
    }

    [Test]
    public async Task ListRepositoriesAsync_talksToTheEndpointUrlNotTheConfiguredHost()
    {
        var handler = Recorded(("GET /user/repos?limit=100", """[{"name":"widgets","owner":{"login":"acme"}}]"""));

        await new ForgejoForgeCapability(handler).ListRepositoriesAsync("token", "https://git.company.test");

        Assert.That(handler.Uris.Single(), Does.StartWith("https://git.company.test/api/v1/"));
    }

    [Test]
    public async Task ReadTokenAccessAsync_refusesATokenTheHostWillNotAccept()
    {
        var handler = Recorded(("GET /user", HttpStatusCode.Unauthorized, ""));

        var access = await new ForgejoForgeCapability(handler).ReadTokenAccessAsync("token", "https://git.example.test");

        Assert.Multiple(() =>
        {
            Assert.That(access.CanListRepositories, Is.False);
            Assert.That(access.CanReadPullRequests, Is.False);
        });
    }

    [Test]
    public async Task ReadTokenAccessAsync_refusesATokenThatCannotListRepositories()
    {
        var handler = Recorded(
            ("GET /user", HttpStatusCode.OK, User),
            ("GET /user/repos?limit=1", HttpStatusCode.Forbidden, ""));

        var access = await new ForgejoForgeCapability(handler).ReadTokenAccessAsync("token", "https://git.example.test");

        Assert.That(access.CanListRepositories, Is.False);
        Assert.That(access.Detail, Does.Contain("list repositories"));
    }

    [Test]
    public async Task ReadTokenAccessAsync_acceptsATokenWithNoRepositoriesYet()
    {
        var handler = Recorded(
            ("GET /user", User),
            ("GET /user/repos?limit=1", "[]"));

        var access = await new ForgejoForgeCapability(handler).ReadTokenAccessAsync("token", "https://git.example.test");

        Assert.That(access.CanListRepositories, Is.True);
        Assert.That(access.CanReadPullRequests, Is.True);
    }

    [Test]
    public async Task ReadTokenAccessAsync_refusesATokenThatCannotReadPullRequests()
    {
        var handler = Recorded(
            ("GET /user", HttpStatusCode.OK, User),
            ("GET /user/repos?limit=1", HttpStatusCode.OK, """[{"name":"widgets","owner":{"login":"acme"}}]"""),
            ("GET /repos/acme/widgets/pulls?state=open&limit=1", HttpStatusCode.Forbidden, ""));

        var access = await new ForgejoForgeCapability(handler).ReadTokenAccessAsync("token", "https://git.example.test");

        Assert.That(access.CanReadPullRequests, Is.False);
        Assert.That(access.Detail, Does.Contain("pull requests"));
    }

    [Test]
    public async Task ReadTokenAccessAsync_probesPullRequestsOnTheFirstRepository()
    {
        var handler = Recorded(
            ("GET /user", User),
            ("GET /user/repos?limit=1", """[{"name":"widgets","owner":{"username":"acme"}}]"""),
            ("GET /repos/acme/widgets/pulls?state=open&limit=1", "[]"));

        var access = await new ForgejoForgeCapability(handler).ReadTokenAccessAsync("token", "https://git.example.test");

        Assert.That(access.CanListRepositories, Is.True);
        Assert.That(access.CanReadPullRequests, Is.True);
    }

    [Test]
    public async Task ReadTokenAccessAsync_talksToTheEndpointUrlNotTheConfiguredHost()
    {
        var handler = Recorded(
            ("GET /user", User),
            ("GET /user/repos?limit=1", "[]"));

        await new ForgejoForgeCapability(handler).ReadTokenAccessAsync("token", "https://git.company.test");

        Assert.That(handler.Uris, Has.All.StartsWith("https://git.company.test/api/v1/"));
    }

    [Test]
    public async Task ListRepositoriesAsync_readsTheOwnerUsernameWhenLoginIsMissing()
    {
        var repositories = await new ForgejoForgeCapability(Recorded(("GET /user/repos?limit=100", """
            [{"name":"widgets","owner":{"username":"acme"}},{"name":"","owner":{"login":"skip"}},{"name":"orphan"}]
            """))).ListRepositoriesAsync("token", "https://git.example.test");

        Assert.That(repositories.Select(repository => $"{repository.Owner}/{repository.Name}"), Is.EqualTo(new[] { "acme/widgets" }));
    }

    [Test]
    public async Task ListRepositoriesAsync_returnsEmptyWhenThePayloadIsNotAnArray()
    {
        var repositories = await new ForgejoForgeCapability(Recorded(("GET /user/repos?limit=100", "{}")))
            .ListRepositoriesAsync("token", "https://git.example.test");

        Assert.That(repositories, Is.Empty);
    }

    [Test]
    public async Task ListPullRequestsAsync_readsRequestedReviewersAndReviews()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?state=open&limit=20", """
                [{"id":9,"number":42,"title":"Product cards","html_url":"https://git.example.test/acme/widgets/pulls/42","requested_reviewers":[{"login":"hector"}]}]
                """),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", """
                [{"user":{"login":"lucy"},"state":"APPROVED","submitted_at":"2026-01-02T00:00:00Z","commit_id":"abc123"}]
                """));

        var pulls = await new ForgejoForgeCapability(handler).ListPullRequestsAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(pulls.Single().RequestedReviewers, Is.EqualTo(new[] { "hector" }));
            Assert.That(pulls.Single().Reviews.Single().Login, Is.EqualTo("lucy"));
            Assert.That(pulls.Single().Reviews.Single().State, Is.EqualTo("APPROVED"));
        });
    }

    [Test]
    public async Task ListPullRequestsAsync_returnsEmptyWhenThePayloadIsNotAnArray()
    {
        var pulls = await new ForgejoForgeCapability(Recorded(
                ("GET /repos/acme/widgets/pulls?state=open&limit=20", "{}")))
            .ListPullRequestsAsync(Request());

        Assert.That(pulls, Is.Empty);
    }

    [Test]
    public async Task ListPullRequestsAsync_keepsAPullWhenItsReviewsCannotBeRead()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?state=open&limit=20", HttpStatusCode.OK, """
                [{"id":9,"number":42,"title":"Product cards","html_url":"https://git.example.test/acme/widgets/pulls/42"}]
                """),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", HttpStatusCode.InternalServerError, ""));

        var pulls = await new ForgejoForgeCapability(handler).ListPullRequestsAsync(Request());

        Assert.That(pulls.Single().Title, Is.EqualTo("Product cards"));
        Assert.That(pulls.Single().Reviews, Is.Empty);
    }

    [Test]
    public async Task ListPullRequestsAsync_skipsAReviewWithNoReviewerAndMissingRequestedReviewers()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?state=open&limit=20", """
                [{"id":9,"number":42,"title":"Product cards","html_url":"https://git.example.test/acme/widgets/pulls/42"}]
                """),
            ("GET /repos/acme/widgets/pulls/42/reviews?limit=100", """
                [{"user":{"id":3},"state":"APPROVED"},{"user":{"login":"lucy"},"state":"COMMENT"}]
                """));

        var pulls = await new ForgejoForgeCapability(handler).ListPullRequestsAsync(Request());

        Assert.That(pulls.Single().RequestedReviewers, Is.Empty);
        Assert.That(pulls.Single().Reviews.Single().Login, Is.EqualTo("lucy"));
    }

    [Test]
    public async Task ListPullRequestsAsync_talksToTheEndpointUrlNotTheConfiguredHost()
    {
        var handler = Recorded(
            ("GET /repos/acme/widgets/pulls?state=open&limit=20", "[]"));

        await new ForgejoForgeCapability(handler).ListPullRequestsAsync(
            new ForgeRequest("token", "acme", "widgets", "topic", "https://git.company.test"));

        Assert.That(handler.Uris.Single(), Does.StartWith("https://git.company.test/api/v1/"));
    }

    private static ForgeRequest Request() => new("token", "acme", "widgets", "topic");

    private static RecordedHandler Recorded()
        => Recorded(Array.Empty<(string Key, HttpStatusCode Status, string Body)>());

    private static RecordedHandler Recorded(params (string Key, string Body)[] responses)
        => Recorded(responses.Select(pair => (pair.Key, HttpStatusCode.OK, pair.Body)).ToArray());

    private static RecordedHandler Recorded(params (string Key, HttpStatusCode Status, string Body)[] responses)
        => new(responses.ToDictionary(
            pair => pair.Key,
            pair => (pair.Status, pair.Body),
            StringComparer.Ordinal));

    private sealed class RecordedHandler(IReadOnlyDictionary<string, (HttpStatusCode Status, string Body)> responses)
        : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> Uris { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            var path = request.RequestUri is null ? "" : request.RequestUri.PathAndQuery;
            const string apiPrefix = "/api/v1";
            if (path.StartsWith(apiPrefix, StringComparison.Ordinal))
                path = path[apiPrefix.Length..];
            var key = $"{request.Method.Method} {path}";
            Requests.Add(key);
            if (request.RequestUri is not null)
                Uris.Add(request.RequestUri.ToString());
            if (!responses.TryGetValue(key, out var recorded))
                return CreateResponse(HttpStatusCode.NotFound);

            return CreateResponse(recorded.Status, recorded.Body);
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

    /// <summary>One issue whose title, body and URL are all null, and one with no body member at all.</summary>
    private const string SparseIssues = """
        [{"id":11,"number":8,"title":null,"body":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":null},
         {"id":12,"number":9,"title":"Restock","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example/acme/widgets/issues/9"}]
        """;

    private const string Assigned = """
        [{"id":7,"number":3,"title":"Assigned","body":"do it","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","html_url":"https://forgejo.example.test/acme/widgets/issues/3","assignees":[{"login":"agentup"}]}]
        """;

    private const string User = """{"login":"agentup","full_name":"Agent Up","html_url":"https://forgejo.example.test/agentup"}""";

    private const string Reviews = """
        [{"user":{"login":"hector"},"state":"REQUEST_CHANGES","submitted_at":"2026-09-22T06:00:00Z"},
         {"user":{"login":"hector"},"state":"APPROVED","submitted_at":"2026-09-22T07:00:00Z"},
         {"user":{"login":"lucy"},"state":"COMMENTED","submitted_at":"2026-09-22T07:30:00Z"}]
        """;

    /// <summary>Each reviewer leaves a decisive verdict and then comments again afterwards.</summary>
    private const string CommentedAfterVerdict = """
        [{"user":{"login":"hector"},"state":"APPROVED","submitted_at":"2026-09-22T06:00:00Z"},
         {"user":{"login":"hector"},"state":"COMMENT","submitted_at":"2026-09-22T08:00:00Z"},
         {"user":{"login":"lucy"},"state":"REQUEST_CHANGES","submitted_at":"2026-09-22T06:30:00Z"},
         {"user":{"login":"lucy"},"state":"COMMENTED","submitted_at":"2026-09-22T08:30:00Z"}]
        """;

    /// <summary>Reviews with no user, a user that is not an object, a user with no login, and two reviewers with neither state nor a usable time.</summary>
    private const string PartialReviews = """
        [{"user":null,"state":"APPROVED","submitted_at":"2026-09-22T06:00:00Z"},
         {"user":"ghost","state":"APPROVED"},
         {"user":{"id":3},"state":"APPROVED"},
         {"state":"APPROVED"},
         {"user":{"login":"mia"},"state":null},
         {"user":{"login":"ola"},"submitted_at":null}]
        """;

    private const string Opened = """
        {"id":1,"number":42,"title":"Title","html_url":"https://forgejo.example.test/acme/widgets/pulls/42","state":"open","head":{"ref":"topic","sha":"abc123"},"base":{"ref":"main"}}
        """;
}
