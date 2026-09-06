using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Database.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/applications/{applicationName}/database")]
public sealed class DatabaseController(DatabaseService databases) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCatalog(string workspaceId, string applicationName, CancellationToken cancellationToken)
    {
        var result = await databases.GetCatalogAsync(workspaceId, applicationName, cancellationToken);
        return MapResult(result, "Database unavailable", 502);
    }

    [HttpPost("query")]
    public async Task<IActionResult> Execute(string workspaceId, string applicationName, [FromBody] DatabaseQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await databases.ExecuteAsync(workspaceId, applicationName, request, cancellationToken);
        return MapResult(result, "Database query failed", 400);
    }

    private IActionResult MapResult<T>(DatabaseOperationResult<T> result, string title, int errorStatus)
    {
        if (!result.Found) return NotFound();
        return MapFoundResult(result, title, errorStatus);
    }

    private IActionResult MapFoundResult<T>(DatabaseOperationResult<T> result, string title, int errorStatus)
        => result.Error is not null
            ? Problem(statusCode: errorStatus, title: title, detail: result.Error)
            : Ok(result.Value);
}
