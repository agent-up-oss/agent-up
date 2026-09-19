using System.Text.Json;

namespace AgentUp.AUDebug.Features.Docs.Providers;

public static class DocsHeadingScriptProvider
{
    public static string Build(string heading)
        => $$"""
            (async () => {
              const wanted = {{JsonSerializer.Serialize(heading)}};
              const normalize = (value) => value.replace(/\s+/g, ' ').trim().toLowerCase();
              const wantedText = normalize(wanted);
              const slug = wantedText.replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-+|-+$/g, '');
              const start = Date.now();
              while (Date.now() - start < 8000) {
              const byId = slug
                ? [...document.querySelectorAll('h1[id],h2[id],h3[id],h4[id],h5[id],h6[id]')].find((node) => node.id === slug)
                  || document.getElementById(slug)
                : null;
              const headings = [...document.querySelectorAll('h1,h2,h3,h4,h5,h6')];
              const byText = headings.find((node) => normalize(node.textContent || '') === wantedText);
              const target = byId || byText;
              if (target) {
                target.scrollIntoView({ block: 'start', inline: 'nearest', behavior: 'instant' });
                const rect = target.getBoundingClientRect();
                window.scrollBy(0, rect.top - 96);
                await new Promise((resolve) => setTimeout(resolve, 250));
                return target.id || (target.textContent || '').trim();
              }
                await new Promise((resolve) => setTimeout(resolve, 100));
              }
              throw new Error('Heading not found: ' + wanted);
            })()
            """;
}
