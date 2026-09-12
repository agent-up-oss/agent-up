using System.Diagnostics;

namespace AgentUp.Server.Features.DesktopApplications.Models;

public sealed record DesktopDisplayHandle(string DisplayName, Process DisplayProcess, int Width, int Height);
