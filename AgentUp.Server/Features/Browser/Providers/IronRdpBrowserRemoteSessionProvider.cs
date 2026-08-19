using AgentUp.Browser.Streaming.DTOs;
using AgentUp.Browser.Streaming.Interfaces;
using AgentUp.Browser.Streaming.Models;
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
            Transport: IsIronRdpBindingAvailable() ? "rdp" : "rdp-unavailable",
            ViewerPath: $"/api/browser/rdp-viewer?workspaceId={Uri.EscapeDataString(workspaceId)}",
            DisplayWebSocketPath: $"/api/browser/rdp/{Uri.EscapeDataString(workspaceId)}",
            LatestFramePath: $"/api/browser/rdp/{Uri.EscapeDataString(workspaceId)}/frame",
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
