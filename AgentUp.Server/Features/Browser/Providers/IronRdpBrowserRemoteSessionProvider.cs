using AgentUp.Server.Features.Browser.DTOs;
using AgentUp.Server.Features.Browser.Interfaces;
using AgentUp.Server.Features.Browser.Models;
using Devolutions.IronRdp;

namespace AgentUp.Server.Features.Browser.Providers;

public sealed class IronRdpBrowserRemoteSessionProvider : IBrowserRemoteSessionProvider
{
    public BrowserRemoteSessionDto GetSession(string workspaceId, BrowserControlMode mode)
    {
        var presets = BrowserViewportPreset.Standard
            .Select(p => new BrowserViewportPresetDto(p.Id, p.Label, p.Width, p.Height))
            .ToArray();

        var selectedPreset = BrowserViewportPreset.Find(mode.Width, mode.Height)
                             ?? BrowserViewportPreset.Default;

        return new BrowserRemoteSessionDto(
            WorkspaceId: workspaceId,
            Transport: IsIronRdpBindingAvailable() ? "ironrdp-preview" : "screencast-fallback",
            ViewerPath: $"/api/browser/viewer?workspaceId={Uri.EscapeDataString(workspaceId)}",
            ControlAuthority: mode.Authority == ControlAuthority.Human ? "human" : "ai",
            Width: mode.Width,
            Height: mode.Height,
            ViewportPresets: presets,
            SelectedPresetId: selectedPreset.Id,
            TouchCapable: true);
    }

    private static bool IsIronRdpBindingAvailable()
        => string.Equals(typeof(ConfigBuilder).Assembly.GetName().Name, "Devolutions.IronRdp", StringComparison.Ordinal);
}
