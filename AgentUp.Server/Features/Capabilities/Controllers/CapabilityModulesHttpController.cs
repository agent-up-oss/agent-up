using AgentUp.Server.Features.Capabilities.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Capabilities.Controllers;

[ApiController]
[Route("api/capabilities")]
public sealed class CapabilityModulesHttpController(CapabilityModulesController modules) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<CapabilityModuleDto> List() => modules.List();

    /// <summary>Enables a built-in capability. Refused with 409 while the software it runs cannot be found.</summary>
    [HttpPost("enable")]
    public ObjectResult Enable([FromBody] EnableCapabilityModuleRequest request)
        => ToResult(modules.Enable(request.Id));

    [HttpPost("disable/{id}")]
    public ObjectResult Disable(string id) => ToResult(modules.Disable(id));

    /// <summary>Changes the listed settings and keeps the rest. Secrets are stored but never returned.</summary>
    [HttpPut("{id}/settings")]
    public ObjectResult UpdateSettings(string id, [FromBody] UpdateCapabilitySettingsRequest request)
        => ToResult(modules.UpdateSettings(id, request.Values ?? new Dictionary<string, string?>()));

    private ObjectResult ToResult(CapabilityChangeResult result) => StatusCode(result.StatusCode, result.Body);
}
