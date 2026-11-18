using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Capabilities.DTOs;

public sealed record CapabilityChangeResult(
    CapabilityChangeStatus Status,
    CapabilityModuleDto? Module,
    IReadOnlyList<string> Errors)
{
    public static CapabilityChangeResult Changed(CapabilityModuleDto module) => new(CapabilityChangeStatus.Changed, module, []);

    public static CapabilityChangeResult NotFound(string id) =>
        new(CapabilityChangeStatus.NotFound, null, [$"Capability '{id}' is not built into this Server."]);

    /// <summary>
    /// 200 with the module; 404 for an id the Server was not built with; 409 when the software is
    /// missing, because enabling cannot succeed until it is found; 400 for a rejected setting.
    /// </summary>
    public int StatusCode => Status switch
    {
        CapabilityChangeStatus.Changed => StatusCodes.Status200OK,
        CapabilityChangeStatus.NotFound => StatusCodes.Status404NotFound,
        CapabilityChangeStatus.Unavailable => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };

    public object? Body => Status switch
    {
        CapabilityChangeStatus.Changed => Module,
        CapabilityChangeStatus.Invalid => new ValidationProblemDetails(new Dictionary<string, string[]> { ["values"] = [.. Errors] })
        {
            Title = "Invalid capability settings",
            Status = StatusCode
        },
        _ => new ProblemDetails
        {
            Title = Status == CapabilityChangeStatus.NotFound ? "Unknown capability" : "Capability software not found",
            Detail = string.Join(" ", Errors),
            Status = StatusCode
        }
    };
}
