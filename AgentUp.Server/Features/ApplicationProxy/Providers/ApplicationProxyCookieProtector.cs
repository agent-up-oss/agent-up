using System.Security.Cryptography;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;
using Microsoft.AspNetCore.DataProtection;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyCookieProtector(IDataProtectionProvider protection) : IApplicationProxyCookieProtector
{
    private readonly IDataProtector _protector = protection.CreateProtector("AgentUp.ApplicationProxy.v1");

    public string Protect(ApplicationProxySession session)
    {
        var payload = $"{session.WorkspaceId}\n{session.AllocatedPort}\n{session.ExpiresAt.ToUnixTimeSeconds()}";
        return _protector.Protect(payload);
    }

    public ApplicationProxySession? Unprotect(string? value, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var payload = _protector.Unprotect(value);
            return Parse(payload, now);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static ApplicationProxySession? Parse(string payload, DateTimeOffset now)
    {
        var parts = payload.Split('\n');
        if (parts.Length != 3)
            return null;
        if (!int.TryParse(parts[1], out var port) || port is < 1 or > 65535)
            return null;
        if (!long.TryParse(parts[2], out var expiresSeconds))
            return null;

        var expires = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds);
        if (expires <= now || string.IsNullOrWhiteSpace(parts[0]))
            return null;

        return new ApplicationProxySession
        {
            WorkspaceId = parts[0],
            AllocatedPort = port,
            ExpiresAt = expires
        };
    }
}
