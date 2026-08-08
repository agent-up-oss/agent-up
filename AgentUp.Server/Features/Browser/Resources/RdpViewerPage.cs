namespace AgentUp.Server.Features.Browser.Resources;

internal static class RdpViewerPage
{
    public static string Build(string workspaceId) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>AgentUp Browser</title>
          <style>
            html, body { margin: 0; padding: 0; background: #1e1e1e; overflow: hidden; width: 100%; height: 100%; }
            #c { display: block; width: 100%; height: 100%; object-fit: contain; }
            #ai-badge {
              display: block; position: fixed; bottom: 8px; right: 10px;
              background: #222; color: #888; font: bold 11px/1 monospace;
              padding: 3px 7px; border-radius: 4px; border: 1px solid #444;
              pointer-events: none; z-index: 9;
            }
          </style>
        </head>
        <body>
        <canvas id="c"></canvas>
        <div id="ai-badge">AI</div>
        <script>
          const workspaceId = {{System.Text.Json.JsonSerializer.Serialize(workspaceId)}};
          const canvas = document.getElementById('c');
          const ctx = canvas.getContext('2d');

          const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
          const streamUrl = `${proto}//${location.host}/api/browser/rdp/${encodeURIComponent(workspaceId)}`;
          let ws = null;
          let reconnectTimer = 0;

          let lastFrameAt = 0;
          let pollTimer = 0;

          function drawBlob(blob) {
            const url = URL.createObjectURL(blob);
            const img = new Image();
            img.onload = () => {
              if (canvas.width !== img.width) canvas.width = img.width;
              if (canvas.height !== img.height) canvas.height = img.height;
              ctx.drawImage(img, 0, 0);
              lastFrameAt = Date.now();
              URL.revokeObjectURL(url);
            };
            img.onerror = () => URL.revokeObjectURL(url);
            img.src = url;
          }

          async function pollFrame() {
            try {
              const res = await fetch(`/api/browser/rdp/${encodeURIComponent(workspaceId)}/frame?t=${Date.now()}`, { cache: 'no-store' });
              if (!res.ok) return;
              drawBlob(await res.blob());
            } catch (_) {}
          }

          function startPolling() {
            if (pollTimer) return;
            pollTimer = window.setInterval(() => {
              if (!lastFrameAt || Date.now() - lastFrameAt > 250) pollFrame();
            }, 250);
          }

          function connectStream() {
            if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) return;
            ws = new WebSocket(streamUrl);
            ws.binaryType = 'arraybuffer';

            ws.onopen = () => {
              if (reconnectTimer) {
                clearTimeout(reconnectTimer);
                reconnectTimer = 0;
              }
            };
            ws.onmessage = (e) => {
              if (typeof e.data !== 'string')
                drawBlob(new Blob([e.data], { type: 'image/jpeg' }));
            };
            ws.onerror = () => startPolling();
            ws.onclose = () => {
              startPolling();
              if (!reconnectTimer)
                reconnectTimer = window.setTimeout(() => {
                  reconnectTimer = 0;
                  connectStream();
                }, 500);
            };
          }

          connectStream();
          startPolling();
          pollFrame();
        </script>
        </body>
        </html>
        """;
}
