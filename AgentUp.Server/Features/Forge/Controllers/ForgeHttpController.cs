using AgentUp.Server.Features.Forge.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Forge.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/forge")]
public sealed class ForgeHttpController(ForgeController forge) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string workspaceId, CancellationToken cancellationToken)
    {
        var hosting = await forge.GetHostingAsync(workspaceId, cancellationToken);
        return hosting is null ? NotFound() : Ok(hosting);
    }

    [HttpGet("issues")]
    public async Task<IActionResult> Issues(string workspaceId, CancellationToken cancellationToken)
    {
        var issues = await forge.ListIssuesAsync(workspaceId, cancellationToken);
        return Ok(issues.Select(issue => new ForgeIssueDto(
            issue.Id,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.Url)));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(string workspaceId, CancellationToken cancellationToken)
    {
        var origin = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var outcome = await forge.StartLoginAsync(workspaceId, origin, cancellationToken);
        return StatusCode(outcome.StatusCode, outcome.Body);
    }

    [HttpPost("logout")]
    public IActionResult Logout(string workspaceId)
        => forge.Logout(workspaceId) ? NoContent() : NotFound();

    [HttpPost("proposals")]
    public async Task<IActionResult> OpenProposal(
        string workspaceId,
        [FromBody] ForgeOpenProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await forge.OpenProposalAsync(workspaceId, request, cancellationToken);
        return proposal is null ? NotFound() : Ok(proposal);
    }

    [HttpPost("refs")]
    public async Task<IActionResult> UpdateRef(
        string workspaceId,
        [FromBody] ForgeUpdateRefRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await forge.UpdateRefAsync(workspaceId, request, cancellationToken);
        return StatusCode(outcome.StatusCode, outcome.Body);
    }

    [HttpPost("comments")]
    public async Task<IActionResult> Comment(
        string workspaceId,
        [FromBody] ForgeCommentRequest request,
        CancellationToken cancellationToken)
    {
        await forge.CommentAsync(workspaceId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("reviews")]
    public async Task<IActionResult> SubmitReview(
        string workspaceId,
        [FromBody] ForgeReviewRequest request,
        CancellationToken cancellationToken)
    {
        await forge.SubmitReviewAsync(workspaceId, request, cancellationToken);
        return NoContent();
    }
}
