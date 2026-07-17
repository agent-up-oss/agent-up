namespace AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;

public sealed record SmokeCommandParseResult(SmokeCommandRequest? Request, string Usage)
{
    public bool Succeeded => Request is not null;
}
