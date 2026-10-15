using AgentUp.Desktop.Features.Agents.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds an <see cref="AgentSessionDto"/> from the canonical Desktop workspace, so a test
/// states only the agent, bind, or loop field it is actually about.
/// </summary>
internal sealed class AgentSessionDtoBuilder
{
    private string _workspaceId = DesktopDomain.WorkspaceId;
    private string? _agent;
    private string _state = "idle";
    private string? _sessionId;
    private string? _error;
    private IReadOnlyList<AgentDescriptorDto> _agents = [];
    private IReadOnlyList<AgentAuthMethodDto>? _authMethods = [];
    private AgentLoginChallengeDto? _loginChallenge;
    private IReadOnlyList<AgentSessionSummaryDto>? _sessions;
    private string? _boundAgent;
    private AgentLoopStatusDto? _loop;

    public AgentSessionDtoBuilder ForWorkspace(string workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    public AgentSessionDtoBuilder WithAgent(string? agent)
    {
        _agent = agent;
        return this;
    }

    public AgentSessionDtoBuilder InState(string state)
    {
        _state = state;
        return this;
    }

    public AgentSessionDtoBuilder WithSessionId(string? sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public AgentSessionDtoBuilder WithError(string? error)
    {
        _error = error;
        return this;
    }

    public AgentSessionDtoBuilder WithAgents(params AgentDescriptorDto[] agents)
    {
        _agents = agents;
        return this;
    }

    public AgentSessionDtoBuilder WithAuthMethods(params AgentAuthMethodDto[] methods)
    {
        _authMethods = methods;
        return this;
    }

    public AgentSessionDtoBuilder WithLoginChallenge(AgentLoginChallengeDto? challenge)
    {
        _loginChallenge = challenge;
        return this;
    }

    public AgentSessionDtoBuilder WithSessions(params AgentSessionSummaryDto[] sessions)
    {
        _sessions = sessions;
        return this;
    }

    public AgentSessionDtoBuilder BoundTo(string? agent)
    {
        _boundAgent = agent;
        return this;
    }

    public AgentSessionDtoBuilder WithLoop(AgentLoopStatusDto? loop)
    {
        _loop = loop;
        return this;
    }

    public AgentSessionDtoBuilder Ready(string agent = "Codex", string sessionId = "s1")
    {
        _agent = agent;
        _state = "ready";
        _sessionId = sessionId;
        if (_agents.Count == 0)
            _agents = [new AgentDescriptorDto(agent, true, agent)];
        return this;
    }

    public AgentSessionDtoBuilder IdleWith(params AgentDescriptorDto[] agents) =>
        WithAgents(agents);

    public AgentSessionDto Build() =>
        new(_workspaceId, _agent, _state, _sessionId, _error, _agents, _authMethods, _loginChallenge, _sessions, _boundAgent, _loop);
}
