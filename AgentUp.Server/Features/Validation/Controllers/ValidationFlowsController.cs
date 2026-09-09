using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Services;
using Microsoft.AspNetCore.Mvc;
namespace AgentUp.Server.Features.Validation.Controllers;
[ApiController, Route("api/workspaces/{workspaceId}/validation-flows")]
public sealed class ValidationFlowsController(ValidationFlowService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(string workspaceId, [FromQuery] string application, CancellationToken ct) => Ok(await service.ListAsync(workspaceId, application, ct));
    [HttpPut]
    public async Task<IActionResult> Save(string workspaceId, SaveValidationFlowRequest request, CancellationToken ct)
    {
        var result = await service.SaveAsync(workspaceId, request, ct);
        return result.Succeeded
            ? Ok(result.Flow)
            : ValidationProblem(detail: result.Error);
    }
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(string workspaceId, string id, CancellationToken ct) => await service.DeleteAsync(workspaceId, id, ct) ? NoContent() : NotFound();
    [HttpPost("{id}/run")] public async Task<IActionResult> Run(string workspaceId, string id, CancellationToken ct) { var result = await service.RunAsync(workspaceId, id, ct); return result.Succeeded ? Ok(result) : Problem(detail: result.Message, statusCode: 422); }
    [HttpGet("{id}/playwright")] public async Task<IActionResult> Export(string workspaceId, string id, CancellationToken ct) { var export = await service.ExportAsync(workspaceId, id, ct); return export is null ? NotFound() : File(System.Text.Encoding.UTF8.GetBytes(export.Content), "text/typescript", export.FileName); }
}
