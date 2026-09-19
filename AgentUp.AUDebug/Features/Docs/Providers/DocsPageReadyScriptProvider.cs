using System.Text.Json;

namespace AgentUp.AUDebug.Features.Docs.Providers;

public static class DocsPageReadyScriptProvider
{
    public static string Build(string url)
        => $$"""
            (async () => {
              const wanted = {{JsonSerializer.Serialize(url)}};
              const start = Date.now();
              while (Date.now() - start < 15000) {
                const href = location.href || '';
                const text = ((document.body && document.body.innerText) || '').trim();
                const marker = document.querySelector('article, main, .navbar, .theme-doc-markdown, .markdown');
                if (href.indexOf('127.0.0.1:10100') >= 0 && (marker || text.length > 20))
                  return href;
                await new Promise((resolve) => setTimeout(resolve, 100));
              }
              throw new Error(
                'Timed out waiting for the docs page to render: href=' + location.href
                + ' ready=' + document.readyState
                + ' wanted=' + wanted);
            })()
            """;
}
