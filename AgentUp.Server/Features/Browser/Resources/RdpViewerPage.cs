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
            #c { display: block; width: 100%; height: 100%; object-fit: contain; cursor: none; touch-action: none; }
            #c.ai-mode { opacity: 0.85; }
            #remote-cursor {
              display: none; position: fixed; left: 0; top: 0; width: 18px; height: 24px;
              pointer-events: none; z-index: 20; transform: translate(-100px, -100px);
              filter: drop-shadow(0 1px 1px rgba(0,0,0,.75));
            }
            #remote-cursor.visible { display: block; }
            #remote-cursor::before {
              content: ""; position: absolute; left: 0; top: 0; width: 15px; height: 21px;
              background: #111; clip-path: polygon(0 0, 0 19px, 5px 14px, 8px 22px, 12px 20px, 9px 12px, 15px 12px, 15px 10px, 10px 10px, 1px 1px, 1px 16px, 5px 11px, 9px 20px, 10px 19px, 7px 12px, 13px 11px);
            }
            #remote-cursor::after {
              content: ""; position: absolute; left: 0; top: 0; width: 15px; height: 21px;
              background: #fff; clip-path: polygon(1px 1px, 1px 17px, 5px 12px, 9px 20px, 10px 19px, 7px 11px, 13px 11px);
            }
            #remote-cursor.pointer { width: 20px; height: 22px; }
            #remote-cursor.pointer::before {
              left: 1px; top: 1px; width: 16px; height: 19px;
              clip-path: polygon(6px 0, 10px 0, 10px 7px, 12px 6px, 14px 7px, 15px 8px, 17px 9px, 18px 11px, 17px 18px, 15px 21px, 7px 21px, 4px 17px, 0 12px, 2px 10px, 6px 14px);
            }
            #remote-cursor.pointer::after {
              left: 2px; top: 2px; width: 14px; height: 17px;
              clip-path: polygon(6px 0, 8px 0, 8px 8px, 10px 7px, 11px 8px, 12px 9px, 14px 10px, 15px 11px, 14px 16px, 12px 19px, 7px 19px, 5px 16px, 1px 11px, 2px 10px, 6px 14px);
            }
            #remote-cursor.text { width: 16px; height: 24px; }
            #remote-cursor.text::before {
              left: 4px; top: 0; width: 8px; height: 24px;
              clip-path: polygon(0 0, 8px 0, 8px 3px, 5px 3px, 5px 21px, 8px 21px, 8px 24px, 0 24px, 0 21px, 3px 21px, 3px 3px, 0 3px);
            }
            #remote-cursor.text::after {
              left: 6px; top: 2px; width: 4px; height: 20px;
              clip-path: polygon(0 0, 4px 0, 4px 2px, 3px 2px, 3px 18px, 4px 18px, 4px 20px, 0 20px, 0 18px, 1px 18px, 1px 2px, 0 2px);
            }
            #remote-cursor.grab { width: 18px; height: 20px; }
            #remote-cursor.grab::before, #remote-cursor.grab::after {
              border-radius: 8px 8px 6px 6px; clip-path: none;
            }
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
        <div id="remote-cursor" aria-hidden="true"></div>
        <div id="ai-badge">AI</div>
        <script>
          const workspaceId = {{System.Text.Json.JsonSerializer.Serialize(workspaceId)}};
          const canvas = document.getElementById('c');
          const ctx = canvas.getContext('2d');
          const badge = document.getElementById('ai-badge');
          const cursor = document.getElementById('remote-cursor');

          const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
          const streamUrl = `${proto}//${location.host}/api/browser/rdp/${encodeURIComponent(workspaceId)}`;
          let ws = null;
          let reconnectTimer = 0;

          let humanMode = false; // server starts in AI mode by default
          let lastFrameAt = 0;
          let pollTimer = 0;
          let pendingMove = null;
          let moveScheduled = false;

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
              if (typeof e.data === 'string') {
                try {
                  const m = JSON.parse(e.data);
                  handleControlMessage(m);
                } catch (_) {}
                return;
              }
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

          function applyMode(isHuman) {
            humanMode = isHuman;
            canvas.classList.toggle('ai-mode', !isHuman);
            badge.classList.toggle('visible', !isHuman);
          }

          function send(obj) {
            if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(obj));
          }

          function applyCursorKind(kind) {
            cursor.classList.remove('pointer', 'text', 'grab');
            if (kind === 'pointer') cursor.classList.add('pointer');
            else if (kind === 'text') cursor.classList.add('text');
            else if (kind === 'grab' || kind === 'grabbing') cursor.classList.add('grab');
          }

          function showCursor(e) {
            cursor.classList.add('visible');
            cursor.style.transform = `translate(${Math.round(e.clientX)}px, ${Math.round(e.clientY)}px)`;
          }

          function hideCursor() {
            cursor.classList.remove('visible');
          }

          function viewerSize() {
            const r = canvas.getBoundingClientRect();
            return { width: Math.max(1, Math.round(r.width)), height: Math.max(1, Math.round(r.height)) };
          }

          function reclaim() {
            if (!humanMode) {
              humanMode = true;
              canvas.classList.remove('ai-mode');
              badge.classList.remove('visible');
              send({ type: 'controlmode', authority: 'human', ...viewerSize() });
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

          function flushMove() {
            moveScheduled = false;
            if (!pendingMove) return;
            send(pendingMove);
            pendingMove = null;
          }

          function scheduleMove(p) {
            pendingMove = { type: 'mousemove', ...p };
            if (moveScheduled) return;
            moveScheduled = true;
            requestAnimationFrame(flushMove);
          }

          canvas.addEventListener('mouseenter', () => {
            canvas.focus({ preventScroll: true });
            reclaim();
          });
          canvas.addEventListener('mouseleave', hideCursor);
          window.addEventListener('blur', hideCursor);
          canvas.addEventListener('mousemove', e => {
            reclaim();
            showCursor(e);
            const p = scale(e);
            scheduleMove(p);
          });
          canvas.addEventListener('mousedown', e => {
            e.preventDefault();
            reclaim();
            showCursor(e);
            const p = scale(e);
            flushMove();
            send({ type: 'mousedown', button: btn(e), ...p });
          });
          canvas.addEventListener('mouseup', e => {
            e.preventDefault();
            const p = scale(e);
            flushMove();
            send({ type: 'mouseup', button: btn(e), ...p });
          });
          canvas.addEventListener('click', e => {
            e.preventDefault();
          });
          canvas.addEventListener('dblclick', e => {
            e.preventDefault();
          });
          canvas.addEventListener('contextmenu', e => {
            e.preventDefault();
          });
          canvas.addEventListener('wheel', e => {
            e.preventDefault();
            reclaim();
            send({ type: 'wheel', deltaX: e.deltaX, deltaY: e.deltaY });
          }, { passive: false });

          canvas.addEventListener('keydown', e => {
            e.preventDefault();
            reclaim();
            send({ type: 'keydown', key: e.key, modifiers: mods(e) });
          });
          canvas.addEventListener('keyup', e => {
            send({ type: 'keyup', key: e.key });
          });

          canvas.addEventListener('paste', e => {
            e.preventDefault();
            const text = e.clipboardData?.getData('text/plain');
            if (text) send({ type: 'type', text });
          });

          function handleControlMessage(m) {
            if (m.type === 'mode') applyMode(m.authority === 'human');
            if (m.type === 'cursor') applyCursorKind(m.cursor);
          }
        </script>
        </body>
        </html>
        """;
}
