using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Docs.Providers;

public static class DocsPageSizeScriptProvider
{
    public static string Build()
        => $$"""
            (() => ({
              width: Math.max(
                document.documentElement.scrollWidth,
                document.body ? document.body.scrollWidth : 0,
                {{DebugLayout.DocsViewportWidth}}),
              height: Math.max(
                document.documentElement.scrollHeight,
                document.body ? document.body.scrollHeight : 0,
                {{DebugLayout.DocsViewportHeight}})
            }))()
            """;
}
