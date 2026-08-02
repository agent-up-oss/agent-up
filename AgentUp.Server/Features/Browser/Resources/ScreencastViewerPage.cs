namespace AgentUp.Server.Features.Browser.Resources;

internal static class ScreencastViewerPage
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
            #c { display: block; width: 100%; height: 100%; object-fit: contain; cursor: crosshair; }
            #c.ai-mode { cursor: not-allowed; opacity: 0.85; }
            #ai-badge {
              display: none; position: fixed; bottom: 8px; right: 10px;
              background: #222; color: #888; font: bold 11px/1 monospace;
              padding: 3px 7px; border-radius: 4px; border: 1px solid #444;
              pointer-events: none; z-index: 9;
            }
            #ai-badge.visible { display: block; }
          </style>
        </head>
        <body>
        <canvas id="c" tabindex="0"></canvas>
        <div id="ai-badge">AI</div>
        <script>
          const workspaceId = {{System.Text.Json.JsonSerializer.Serialize(workspaceId)}};
          const canvas = document.getElementById('c');
          const ctx = canvas.getContext('2d');
          const badge = document.getElementById('ai-badge');

          const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
          const ws = new WebSocket(`${proto}//${location.host}/api/browser/screencast/${encodeURIComponent(workspaceId)}`);
          ws.binaryType = 'arraybuffer';

          let humanMode = false; // server starts in AI mode by default

          ws.onmessage = (e) => {
            if (typeof e.data === 'string') {
              try {
                const m = JSON.parse(e.data);
                if (m.type === 'mode') applyMode(m.authority === 'human');
              } catch (_) {}
              return;
            }
            const blob = new Blob([e.data], { type: 'image/jpeg' });
            const url = URL.createObjectURL(blob);
            const img = new Image();
            img.onload = () => {
              if (canvas.width !== img.width) canvas.width = img.width;
              if (canvas.height !== img.height) canvas.height = img.height;
              ctx.drawImage(img, 0, 0);
              URL.revokeObjectURL(url);
            };
            img.src = url;
          };

          ws.onclose = () => {
            ctx.fillStyle = '#1e1e1e';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            setTimeout(() => location.reload(), 2000);
          };

          function applyMode(isHuman) {
            humanMode = isHuman;
            canvas.classList.toggle('ai-mode', !isHuman);
            badge.classList.toggle('visible', !isHuman);
          }

          function send(obj) {
            if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(obj));
          }

          function reclaim() {
            if (!humanMode) {
              humanMode = true;
              canvas.classList.remove('ai-mode');
              badge.classList.remove('visible');
              send({ type: 'controlmode', authority: 'human' });
            }
          }

          function scale(e) {
            const r = canvas.getBoundingClientRect();
            const scaleX = canvas.width / r.width;
            const scaleY = canvas.height / r.height;
            return { x: Math.round((e.clientX - r.left) * scaleX), y: Math.round((e.clientY - r.top) * scaleY) };
          }

          function btn(e) { return ['left', 'middle', 'right'][e.button] ?? 'left'; }
          function mods(e) {
            const m = [];
            if (e.ctrlKey)  m.push('ctrl');
            if (e.shiftKey) m.push('shift');
            if (e.altKey)   m.push('alt');
            if (e.metaKey)  m.push('meta');
            return m;
          }

          canvas.addEventListener('mouseenter', () => canvas.focus({ preventScroll: true }));
          canvas.addEventListener('mousemove', e => {
            if (!humanMode) return;
            const p = scale(e);
            send({ type: 'mousemove', ...p });
          });
          canvas.addEventListener('mousedown', e => {
            reclaim();
            const p = scale(e);
            send({ type: 'mousedown', button: btn(e), ...p });
          });
          canvas.addEventListener('mouseup', e => {
            const p = scale(e);
            send({ type: 'mouseup', button: btn(e), ...p });
          });
          canvas.addEventListener('click', e => {
            const p = scale(e);
            send({ type: 'click', button: btn(e), clickCount: e.detail, ...p });
          });
          canvas.addEventListener('dblclick', e => {
            const p = scale(e);
            send({ type: 'click', button: 'left', clickCount: 2, ...p });
          });
          canvas.addEventListener('contextmenu', e => {
            e.preventDefault();
            const p = scale(e);
            send({ type: 'mousedown', button: 'right', ...p });
          });
          canvas.addEventListener('wheel', e => {
            e.preventDefault();
            send({ type: 'wheel', deltaX: e.deltaX, deltaY: e.deltaY });
          }, { passive: false });

          canvas.addEventListener('keydown', e => {
            e.preventDefault();
            send({ type: 'keydown', key: e.key, modifiers: mods(e) });
          });
          canvas.addEventListener('keyup', e => {
            send({ type: 'keyup', key: e.key });
          });

          // Paste support: send text content as type events
          canvas.addEventListener('paste', e => {
            e.preventDefault();
            const text = e.clipboardData?.getData('text/plain');
            if (text) send({ type: 'type', text });
          });
        </script>
        </body>
        </html>
        """;
}
