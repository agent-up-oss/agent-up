using AgentUp.Server.Features.Database.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Database.Services;

public sealed class DatabasePresentationService
{
    public IActionResult Present<T>(DatabaseExplorerResult<T> result)
        => result.Status switch
        {
            DatabaseExplorerStatus.Success => new OkObjectResult(result.Value),
            DatabaseExplorerStatus.NotFound => new NotFoundResult(),
            DatabaseExplorerStatus.BadRequest => new ObjectResult(new ProblemDetails
            {
                Detail = result.Detail,
                Status = StatusCodes.Status400BadRequest
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            },
            _ => new ObjectResult(new ProblemDetails { Status = StatusCodes.Status500InternalServerError })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            }
        };

    public IActionResult ValidationProblem(string field, string message)
        => new BadRequestObjectResult(new ValidationProblemDetails(
            new Dictionary<string, string[]> { [field] = [message] }));
}
