using AgentUp.Server.Features.Files.DTOs;
using AgentUp.Server.Features.Files.Models;
using AgentUp.Server.Features.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Files.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/files")]
public sealed class WorkspaceFilesController(WorkspaceFilesService files) : ControllerBase
{
    [HttpGet("directory")]
    public async Task<IActionResult> GetDirectory(string workspaceId, [FromQuery] string? path)
        => Answer(await files.GetDirectoryAsync(workspaceId, path, HttpContext.RequestAborted));

    [HttpGet("file")]
    public async Task<IActionResult> GetFile(string workspaceId, [FromQuery] string? path)
        => Answer(await files.GetFileAsync(workspaceId, path, HttpContext.RequestAborted));

    [HttpPut("file")]
    public async Task<IActionResult> PutFile(string workspaceId, WorkspaceFileWriteRequest request)
        => Answer(await files.WriteFileAsync(workspaceId, request, HttpContext.RequestAborted));

    private IActionResult Answer<T>(WorkspaceFileResult<T> result) where T : class
        => result.Value is { } value ? Ok(value) : Problem(detail: result.Error, statusCode: result.StatusCode, title: result.Title);
}
