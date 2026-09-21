using AgentUp.Server.Features.Connection.DTOs;
using AgentUp.Server.Features.Connection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Connection.Controllers;

[ApiController]
[Route("api/connection")]
[AllowAnonymous]
public sealed class ConnectionController(ConnectionService connection) : ControllerBase
{
    [HttpGet]
    public ActionResult<ConnectionMetadataDto> Get() => connection.Current();
}
