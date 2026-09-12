using System.Text.Json;

namespace AgentUp.AUDebug.Features.Mobile.Providers;

public static class MobileLoginScriptProvider
{
    public static string Build(string serverUrl, string password)
        => $$"""
            (async () => {
              const setValue = (el, value) => {
                const proto = el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype;
                const desc = Object.getOwnPropertyDescriptor(proto, 'value');
                desc.set.call(el, value);
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
              };
              const waitFor = async (selector, timeoutMs) => {
                const start = Date.now();
                while (Date.now() - start < timeoutMs) {
                  const node = document.querySelector(selector);
                  if (node) return node;
                  await new Promise((resolve) => setTimeout(resolve, 100));
                }
                throw new Error('Timed out waiting for ' + selector);
              };
              const clickByText = (text) => {
                const button = [...document.querySelectorAll('[role="button"],button')].find((node) => (node.textContent || '').includes(text));
                if (!button) throw new Error('Could not find button ' + text);
                button.click();
              };
              const url = await waitFor('input[aria-label="Server URL"], textarea[aria-label="Server URL"]', 10000);
              setValue(url, {{JsonSerializer.Serialize(serverUrl)}});
              clickByText('Try and save');
              const secret = await waitFor('input[aria-label="Admin password"]', 10000);
              setValue(secret, {{JsonSerializer.Serialize(password)}});
              clickByText('Sign in');
              return 'ok';
            })()
            """;
}
