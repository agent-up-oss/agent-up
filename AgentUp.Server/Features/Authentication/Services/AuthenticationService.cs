using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;

namespace AgentUp.Server.Features.Authentication.Services;

public sealed class AuthenticationService(AuthenticationProvider authentication)
{
    public LoginResponse Status() => new(authentication.IsRequired);

    public LoginResponse? Login(string password)
    {
        if (!authentication.IsRequired) return new LoginResponse(false);
        var token = authentication.Login(password);
        return token is null ? null : new LoginResponse(true, token);
    }
}
