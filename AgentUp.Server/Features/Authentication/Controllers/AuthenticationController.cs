using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Authentication.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthenticationController(AuthenticationProvider authentication) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<LoginResponse> Status() => new LoginResponse(authentication.IsRequired);

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        if (!authentication.IsRequired) return new LoginResponse(false);
        var token = authentication.Login(request.Password);
        return token is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid password")
            : new LoginResponse(true, token);
    }
}
