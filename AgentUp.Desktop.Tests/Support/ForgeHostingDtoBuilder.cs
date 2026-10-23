using AgentUp.Desktop.Features.Git.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds Git hosting state so tests name availability, detail, and remedy instead of
/// restating the eight-field production constructor.
/// </summary>
internal sealed class ForgeHostingDtoBuilder
{
    private string _availability = "notInstalled";
    private string? _moduleId;
    private ForgeProposalDto? _proposal;
    private IReadOnlyList<ForgeCheckDto> _checks = [];
    private IReadOnlyList<ForgeReviewThreadDto> _threads = [];
    private ForgeMergeabilityDto? _mergeability;
    private string? _detail = "No forge capability package is enabled.";
    private string? _remedy = "Run: agent-up capabilities bootstrap";

    public ForgeHostingDtoBuilder NotInstalled()
    {
        _availability = "notInstalled";
        _moduleId = null;
        _proposal = null;
        _checks = [];
        _threads = [];
        _mergeability = null;
        _detail = "No forge capability package is enabled.";
        _remedy = "Run: agent-up capabilities bootstrap";
        return this;
    }

    public ForgeHostingDtoBuilder NeedsCredential(string moduleId = "github")
    {
        _availability = "needsCredential";
        _moduleId = moduleId;
        _proposal = null;
        _checks = [];
        _threads = [];
        _mergeability = null;
        _detail = "This workspace has no repository host credential.";
        _remedy = "Sign in to your repository host.";
        return this;
    }

    public ForgeHostingDtoBuilder NoRemote(string moduleId = "github")
    {
        _availability = "noRemote";
        _moduleId = moduleId;
        _proposal = null;
        _checks = [];
        _threads = [];
        _mergeability = null;
        _detail = "No repository host origin is configured.";
        _remedy = "Add a git remote for this worktree.";
        return this;
    }

    public ForgeHostingDtoBuilder Unreachable(string? detail = "Could not reach the host.", string moduleId = "github")
    {
        _availability = "unreachable";
        _moduleId = moduleId;
        _proposal = null;
        _checks = [];
        _threads = [];
        _mergeability = null;
        _detail = detail;
        _remedy = "Check the repository host URL and retry.";
        return this;
    }

    public ForgeHostingDtoBuilder NoProposal(string moduleId = "github")
    {
        _availability = "noProposal";
        _moduleId = moduleId;
        _proposal = null;
        _checks = [];
        _threads = [];
        _mergeability = null;
        _detail = "No open proposal on this branch.";
        _remedy = null;
        return this;
    }

    public ForgeHostingDtoBuilder Ready(string moduleId = "github")
    {
        _availability = "ready";
        _moduleId = moduleId;
        _proposal ??= new ForgeProposalDto("1", "42", "Ready", "https://example.test/42", "topic", "main", "open");
        _detail = null;
        _remedy = null;
        return this;
    }

    public ForgeHostingDtoBuilder WithAvailability(string availability)
    {
        _availability = availability;
        return this;
    }

    public ForgeHostingDtoBuilder WithModule(string? moduleId)
    {
        _moduleId = moduleId;
        return this;
    }

    public ForgeHostingDtoBuilder WithDetail(string? detail)
    {
        _detail = detail;
        return this;
    }

    public ForgeHostingDtoBuilder WithRemedy(string? remedy)
    {
        _remedy = remedy;
        return this;
    }

    public ForgeHostingDtoBuilder Titled(string title)
    {
        _proposal = new ForgeProposalDto(
            _proposal?.Id ?? "1",
            _proposal?.Number ?? "42",
            title,
            _proposal?.Url ?? "https://example.test/42",
            _proposal?.HeadRef ?? "topic",
            _proposal?.BaseRef ?? "main",
            _proposal?.State ?? "open");
        return this;
    }

    public ForgeHostingDtoBuilder WithProposal(ForgeProposalDto? proposal)
    {
        _proposal = proposal;
        return this;
    }

    public ForgeHostingDtoBuilder WithChecks(params ForgeCheckDto[] checks)
    {
        _checks = checks;
        return this;
    }

    public ForgeHostingDtoBuilder WithThreads(params ForgeReviewThreadDto[] threads)
    {
        _threads = threads;
        return this;
    }

    public ForgeHostingDtoBuilder Mergeable(bool mergeable = true, string state = "clean")
    {
        _mergeability = new ForgeMergeabilityDto(mergeable, state);
        return this;
    }

    public ForgeHostingDtoBuilder UnknownMergeability()
    {
        _mergeability = null;
        return this;
    }

    public ForgeHostingDto Build()
        => new(_availability, _moduleId, _proposal, _checks, _threads, _mergeability, _detail, _remedy);
}
