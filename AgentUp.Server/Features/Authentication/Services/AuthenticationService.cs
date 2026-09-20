using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Models;
using AgentUp.Server.Features.Authentication.Providers;

namespace AgentUp.Server.Features.Authentication.Services;

public sealed class AuthenticationService(
    AuthenticationProvider authentication,
    AuthenticationModeProvider modes)
{
    public LoginResponse Status() => new(modes.Current != AuthenticationMode.Disabled);

    public LoginResponse? Login(LoginRequest request)
    {
        if (modes.Current == AuthenticationMode.Disabled)
            return new LoginResponse(false);
        if (modes.Current != AuthenticationMode.LocalAdministrator)
            return null;

        var token = authentication.Login(request.Password);
        return token is null ? null : new LoginResponse(true, token);
    }
}
