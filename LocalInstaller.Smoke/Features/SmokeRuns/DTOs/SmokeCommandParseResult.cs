namespace AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;

public sealed record SmokeCommandParseResult(SmokeCommandRequest? Request, string Usage, bool HelpRequested = false)
{
    public bool Succeeded => Request is not null;
}
