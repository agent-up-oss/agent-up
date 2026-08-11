using LocalInstaller.Smoke.Features.SmokeRuns.DTOs;

namespace LocalInstaller.Smoke.Features.SmokeRuns.Interfaces;

public interface ISmokeCommandParser
{
    SmokeCommandParseResult Parse(string[] args);
}
