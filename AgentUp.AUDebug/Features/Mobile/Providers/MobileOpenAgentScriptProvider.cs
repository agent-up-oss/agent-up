namespace AgentUp.AUDebug.Features.Mobile.Providers;

public static class MobileOpenAgentScriptProvider
{
    public static string Build()
        => """
            (async () => {
              const waitForText = async (text, timeoutMs) => {
                const start = Date.now();
                while (Date.now() - start < timeoutMs) {
                  if ((document.body.innerText || '').includes(text)) return;
                  await new Promise((resolve) => setTimeout(resolve, 100));
                }
                throw new Error('Timed out waiting for ' + text);
              };
              const clickByText = (text) => {
                const button = [...document.querySelectorAll('[role="button"],a,button')].find((node) => (node.textContent || '').includes(text));
                if (!button) throw new Error('Could not find ' + text);
                button.click();
              };
              await waitForText('Open chat', 15000);
              clickByText('Open chat');
              return 'ok';
            })()
            """;
}
