namespace AgentUp.Capabilities.Common.Features.NixRuntime.DTOs;

public sealed record NixLaunchWrap(string FileName, IReadOnlyList<string> Arguments);
