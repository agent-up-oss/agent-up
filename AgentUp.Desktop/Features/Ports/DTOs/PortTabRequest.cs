namespace AgentUp.Desktop.Features.Ports.DTOs;

public sealed record PortTabRequest(
    string Variable,
    int DefaultPort,
    int AllocatedPort,
    string Protocol);
