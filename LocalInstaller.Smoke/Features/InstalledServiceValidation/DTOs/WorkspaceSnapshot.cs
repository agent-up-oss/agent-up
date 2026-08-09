using System.Text.Json;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;

internal readonly record struct WorkspaceSnapshot(string Id, JsonElement Json);
