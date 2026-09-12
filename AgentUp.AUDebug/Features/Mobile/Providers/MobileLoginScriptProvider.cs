using System.Text.Json;

namespace AgentUp.AUDebug.Features.Mobile.Providers;

public static class MobileLoginScriptProvider
{
    public static string Build(string serverUrl, string password)
        => $$"""
            (async () => {
              const setValue = (el, value) => {
                el.focus();
                el.select?.();
                if (document.execCommand && document.execCommand('insertText', false, value) && el.value === value) {
                  el.dispatchEvent(new Event('input', { bubbles: true }));
                  el.dispatchEvent(new Event('change', { bubbles: true }));
                  return;
                }
                const proto = el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype;
                const desc = Object.getOwnPropertyDescriptor(proto, 'value');
                const last = el.value;
                desc.set.call(el, value);
                const tracker = el._valueTracker;
                if (tracker && typeof tracker.setValue === 'function') tracker.setValue(last);
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
                if (button.getAttribute('aria-disabled') === 'true') throw new Error('Button is disabled: ' + text);
                button.click();
              };
              const url = await waitFor('input[aria-label="Server URL"], textarea[aria-label="Server URL"]', 10000);
              setValue(url, {{JsonSerializer.Serialize(serverUrl)}});
              const waitEnabled = async (text, timeoutMs) => {
                const start = Date.now();
                while (Date.now() - start < timeoutMs) {
                  setValue(url, {{JsonSerializer.Serialize(serverUrl)}});
                  const button = [...document.querySelectorAll('[role="button"],button')].find((node) => (node.textContent || '').includes(text));
                  if (button && button.getAttribute('aria-disabled') !== 'true') return button;
                  await new Promise((resolve) => setTimeout(resolve, 100));
                }
                throw new Error('Timed out waiting for enabled button ' + text);
              };
              const save = await waitEnabled('Try and save', 8000);
              save.click();
              const secret = await waitFor('input[aria-label="Admin password"]', 20000);
              setValue(secret, {{JsonSerializer.Serialize(password)}});
              clickByText('Sign in');
              return 'ok';
            })()
            """;
}
