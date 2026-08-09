using System.Text.Json;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;

internal readonly record struct WorkspaceSnapshot(string Id, JsonElement Json);
