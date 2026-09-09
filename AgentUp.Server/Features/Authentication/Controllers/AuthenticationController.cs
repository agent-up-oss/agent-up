using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Authentication.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthenticationController(AuthenticationService authentication) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<LoginResponse> Status() => authentication.Status();

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var response = authentication.Login(request.Password);
        return response is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid password")
            : response;
    }
}
