using System.Text;
using AgentUp.Server.Features.Training.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Training.Controllers;

[ApiController]
[Route("api/training")]
public sealed class TrainingHttpController(TrainingController training) : ControllerBase
{
    /// <summary>The repository tiles the Train tab opens on.</summary>
    [HttpGet("repositories")]
    public ActionResult<IReadOnlyList<DecisionRepositoryDto>> Repositories() => Ok(training.Repositories());

    /// <summary>The decisions in one repository, narrowed to a category when one is named.</summary>
    [HttpGet("decisions")]
    public ActionResult<IReadOnlyList<ProductDecisionDto>> Decisions(
        [FromQuery] string? owner = null,
        [FromQuery] string? repository = null,
        [FromQuery] string? category = null,
        [FromQuery] bool includeReviewed = true)
        => Ok(training.List(owner, repository, category, includeReviewed));

    /// <summary>Writes down a decision. Agents normally reach this through MCP rather than here.</summary>
    [HttpPost("decisions")]
    public async Task<ActionResult<RecordDecisionResult>> Record(
        [FromBody] RecordDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await training.RecordAsync(request, cancellationToken: cancellationToken);
        return result.Recorded ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// A picture behind a decision. Served from the backlog's own store rather than from the
    /// capture it came from, so a surface can still show it long after that run.
    /// </summary>
    [HttpGet("evidence/{evidenceId}")]
    public async Task<ActionResult<DecisionEvidenceImageDto>> Evidence(string evidenceId, CancellationToken cancellationToken)
    {
        var image = await training.EvidenceAsync(evidenceId, cancellationToken);
        return image is null
            ? NotFound()
            : Ok(new DecisionEvidenceImageDto(evidenceId, image.Value.MimeType, Convert.ToBase64String(image.Value.Bytes)));
    }

    /// <summary>What the product owner made of one decision.</summary>
    [HttpPost("decisions/{id}/review")]
    public ActionResult<ProductDecisionDto> Review(string id, [FromBody] ReviewDecisionRequest request)
    {
        var reviewed = training.Review(id, request);
        return reviewed is null
            ? Problem(
                statusCode: 404,
                title: "The decision was not reviewed",
                detail: "No decision has that id, or the verdict was not 'good', 'bad' or 'pending'.")
            : Ok(reviewed);
    }

    /// <summary>
    /// The backlog as training material, one decision per line. Downloads rather than renders:
    /// this is a file somebody feeds to a training run.
    /// </summary>
    [HttpGet("export")]
    public IActionResult Export([FromQuery] bool includePending = false)
        => File(
            Encoding.UTF8.GetBytes(training.Export(includePending)),
            training.ExportMimeType,
            training.ExportFileName);
}
