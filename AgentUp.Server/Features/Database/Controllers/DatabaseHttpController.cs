using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Database.Controllers;

[ApiController]
[Route("api/workspaces")]
public sealed class DatabaseHttpController(
    DatabaseExplorerService explorer,
    DatabasePresentationService presentation) : ControllerBase
{
    [HttpGet("{id}/applications/{name}/database/databases")]
    public async Task<IActionResult> ListDatabases(string id, string name, CancellationToken cancellationToken)
        => presentation.Present(await explorer.ListDatabasesAsync(id, name, cancellationToken));

    [HttpGet("{id}/applications/{name}/database/tables")]
    public async Task<IActionResult> ListTables(
        string id,
        string name,
        [FromQuery] string database,
        CancellationToken cancellationToken)
        => presentation.Present(await explorer.ListTablesAsync(id, name, database, cancellationToken));

    [HttpPost("{id}/applications/{name}/database/query")]
    public async Task<IActionResult> ExecuteQuery(
        string id,
        string name,
        [FromBody] DatabaseQueryRequestDto request,
        CancellationToken cancellationToken)
        => presentation.Present(await explorer.ExecuteQueryAsync(id, name, request, cancellationToken));
}
