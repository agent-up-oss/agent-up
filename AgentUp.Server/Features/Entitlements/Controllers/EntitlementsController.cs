using System.Security.Claims;
using AgentUp.Server.Features.Entitlements.DTOs;
using AgentUp.Server.Features.Entitlements.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Entitlements.Controllers;

[ApiController]
[Route("api/entitlements")]
public sealed class EntitlementsController(EntitlementsService entitlements) : ControllerBase
{
    [HttpGet]
    public ActionResult<EntitlementsDto> Get()
        => entitlements.ForSubject(Subject());

    private string Subject()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.Identity?.Name
            ?? "admin";
}
