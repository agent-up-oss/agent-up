using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

public interface ISmokeCommandParser
{
    SmokeCommandParseResult Parse(string[] args);
}
