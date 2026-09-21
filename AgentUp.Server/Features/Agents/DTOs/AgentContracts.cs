using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace AgentUp.Server.Features.Agents.DTOs;

public enum AgentKind { Codex, Cursor, Claude }

public sealed record ScheduleAgentRequest(AgentKind Agent);
public sealed record AgentPromptRequest([Required, MaxLength(100_000)] string Message);
public sealed record AgentPermissionResponse(
    [Required, MinLength(1)] string RequestId,
    [Required, MinLength(1)] string OptionId);
public sealed record AgentAuthenticationRequest([Required, MinLength(1)] string MethodId);
public sealed record AgentLoginCodeRequest([Required, MinLength(1), MaxLength(4096)] string Code);
public sealed record AgentLoginCallbackRequest([Required, MinLength(1), MaxLength(8192)] string Url);
public sealed record AgentAuthMethodDto(string Id, string Name, string? Description);
/// <summary>
/// How the user completes a sign-in, and therefore what the client has to do about it.
/// The client branches on this and never on which agent it is talking to, so a test agent
/// and the real Claude, Codex, or Cursor CLI drive the exact same client code path.
/// </summary>
public enum AgentLoginTransport
{
    /// <summary>No challenge has been observed yet.</summary>
    Unknown,

    /// <summary>
    /// The CLI polls the provider on its own. The client only has to show or open the URL;
    /// there is nothing to send back. Cursor signs in this way.
    /// </summary>
    Poll,

    /// <summary>
    /// The user carries a value between the browser and the CLI: either a user code typed
    /// into the provider page (device authorization) or an authorization code pasted back
    /// out of it. Nothing listens on a socket, so this works unchanged on a phone.
    /// </summary>
    Code,

    /// <summary>
    /// The provider redirects to a loopback address the CLI is listening on. That address is
    /// on the Server host, so whenever the browser is somewhere else the client has to catch
    /// the redirect and hand it back through <c>POST agent/login/callback</c>.
    /// </summary>
    Redirect
}

public sealed record AgentLoginChallengeDto(
    string? Url,
    string? Code,
    string? Instructions,
    AgentLoginTransport Transport = AgentLoginTransport.Unknown,
    bool CanSubmitCode = false,
    DateTimeOffset? ExpiresAt = null,
    string? RedirectUri = null);
public sealed record AgentDescriptor(AgentKind Agent, bool Available, string DisplayName);
public sealed record AgentSessionSummaryDto(string SessionId, AgentKind Agent, string Description, string Branch, DateTimeOffset LastUsedAt);
public sealed record AgentSessionDto(
    string WorkspaceId,
    AgentKind? Agent,
    string State,
    string? SessionId,
    string? Error,
    IReadOnlyList<AgentDescriptor> Agents,
    IReadOnlyList<AgentAuthMethodDto> AuthMethods,
    AgentLoginChallengeDto? LoginChallenge = null,
    IReadOnlyList<AgentSessionSummaryDto>? Sessions = null);
public sealed record AgentEventDto(long Sequence, string Type, JsonElement Payload, DateTimeOffset Timestamp);
public sealed record AgentScheduleResult(AgentSessionDto? Session, bool Found, string? Error);
public sealed record AgentActionResult(bool Found, string? Error)
{
    public static AgentActionResult Success() => new(true, null);
    public static AgentActionResult NotFound() => new(false, null);
    public static AgentActionResult Failed(string error) => new(true, error);
}
