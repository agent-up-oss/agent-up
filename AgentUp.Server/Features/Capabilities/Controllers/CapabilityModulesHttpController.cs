using AgentUp.Server.Features.Capabilities.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Capabilities.Controllers;

[ApiController]
[Route("api/capabilities")]
public sealed class CapabilityModulesHttpController(CapabilityModulesController modules) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<CapabilityModuleDto> List() => modules.List();

    [HttpPost("enable")]
    public CapabilityModuleDto Enable([FromBody] EnableCapabilityModuleRequest request)
        => modules.Enable(request.Id, request.Version);

    [HttpPost("disable/{id}")]
    public CapabilityModuleDto Disable(string id) => modules.Disable(id);
}
