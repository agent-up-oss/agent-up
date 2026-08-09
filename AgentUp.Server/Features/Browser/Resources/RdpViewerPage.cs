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
          // Random per-page-load identifier so audit heartbeats can distinguish concurrent
          // JS contexts (e.g. if the desktop has more than one viewer WebView loading this
          // same URL, or if ReloadWebView leaks old pages).
          const pageInstanceId = Math.random().toString(36).slice(2, 10) + Math.random().toString(36).slice(2, 10);
          const pageLoadedAt = Date.now();
          const canvas = document.getElementById('c');
          const ctx = canvas.getContext('2d');

          const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
          const streamUrl = `${proto}//${location.host}/api/browser/rdp/${encodeURIComponent(workspaceId)}`;
          let ws = null;
          let reconnectTimer = 0;

          // Diagnostic state — sent to /api/audit/record every heartbeatMs so the audit
          // trail shows whether frames are actually reaching the viewer's canvas. This is
          // the only visibility we have into the WebKit-embedded viewer runtime.
          let framesReceived = 0;
          let lastFrameAt = 0;
          let lastPollStatus = '';
          let lastPollError = '';
          let lastWsEvent = 'none';
          let pollTimer = 0;

          function drawBlob(blob) {
            const url = URL.createObjectURL(blob);
            const img = new Image();
            img.onload = () => {
              if (canvas.width !== img.width) canvas.width = img.width;
              if (canvas.height !== img.height) canvas.height = img.height;
              ctx.drawImage(img, 0, 0);
              lastFrameAt = Date.now();
              framesReceived += 1;
              URL.revokeObjectURL(url);
            };
            img.onerror = () => URL.revokeObjectURL(url);
            img.src = url;
          }

          async function pollFrame() {
            try {
              const res = await fetch(`/api/browser/rdp/${encodeURIComponent(workspaceId)}/frame?t=${Date.now()}`, { cache: 'no-store' });
              lastPollStatus = String(res.status);
              lastPollError = '';
              if (!res.ok) return;
              drawBlob(await res.blob());
            } catch (err) {
              lastPollStatus = 'error';
              lastPollError = String((err && err.message) || err || '').slice(0, 200);
            }
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
              lastWsEvent = 'open';
              if (reconnectTimer) {
                clearTimeout(reconnectTimer);
                reconnectTimer = 0;
              }
            };
            ws.onmessage = (e) => {
              if (typeof e.data !== 'string')
                drawBlob(new Blob([e.data], { type: 'image/jpeg' }));
            };
            ws.onerror = () => { lastWsEvent = 'error'; startPolling(); };
            ws.onclose = () => {
              lastWsEvent = 'close';
              startPolling();
              if (!reconnectTimer)
                reconnectTimer = window.setTimeout(() => {
                  reconnectTimer = 0;
                  connectStream();
                }, 500);
            };
          }

          const heartbeatMs = 3000;
          let heartbeatCount = 0;
          function wsStateName(state) {
            if (state === WebSocket.CONNECTING) return 'connecting';
            if (state === WebSocket.OPEN) return 'open';
            if (state === WebSocket.CLOSING) return 'closing';
            if (state === WebSocket.CLOSED) return 'closed';
            return 'none';
          }

          // Post a marker event to the audit trail. Used from lifecycle hooks (teardown,
          // errors, visibility changes) so we can see WHY the heartbeat interval died.
          function auditMarker(action, outcome, extra) {
            try {
              const details = Object.assign({
                pageInstanceId: pageInstanceId,
                pageAgeMs: String(Date.now() - pageLoadedAt),
                heartbeatCount: String(heartbeatCount),
              }, extra || {});
              fetch('/api/audit/record', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                  Kind: 'stream', Source: 'viewer', Action: action,
                  Outcome: outcome, WorkspaceId: workspaceId, Details: details,
                }),
                cache: 'no-store', keepalive: true,
              }).catch(() => {});
            } catch (_) {}
          }

          window.addEventListener('error', (e) => {
            auditMarker('js_error', 'error', {
              message: String(e && e.message || '').slice(0, 200),
              filename: String(e && e.filename || '').slice(0, 200),
              lineno: String(e && e.lineno || ''),
            });
          });
          window.addEventListener('unhandledrejection', (e) => {
            auditMarker('js_error', 'unhandled_rejection', {
              reason: String((e && e.reason && e.reason.message) || e.reason || '').slice(0, 200),
            });
          });
          document.addEventListener('visibilitychange', () => {
            auditMarker('visibility_change', document.visibilityState || 'unknown');
          });

          function sendHeartbeat() {
            heartbeatCount += 1;
            const now = Date.now();
            const outcome = framesReceived > 0 && (now - lastFrameAt) < heartbeatMs * 2
              ? 'streaming' : 'idle';
            const payload = {
              Kind: 'stream',
              Source: 'viewer',
              Action: 'heartbeat',
              Outcome: outcome,
              WorkspaceId: workspaceId,
              Details: {
                pageInstanceId: pageInstanceId,
                pageAgeMs: String(now - pageLoadedAt),
                documentVisibilityState: document.visibilityState || 'unknown',
                framesReceived: String(framesReceived),
                lastFrameAgoMs: lastFrameAt ? String(now - lastFrameAt) : '',
                canvasWidth: String(canvas.width),
                canvasHeight: String(canvas.height),
                wsReadyState: wsStateName(ws && ws.readyState),
                lastWsEvent: lastWsEvent,
                lastPollStatus: lastPollStatus,
                lastPollError: lastPollError,
                streamUrl: streamUrl,
                viewportWidth: String(window.innerWidth),
                viewportHeight: String(window.innerHeight),
              }
            };
            try {
              fetch('/api/audit/record', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
                cache: 'no-store',
                keepalive: true,
              }).catch(() => {});
            } catch (_) {}
          }
          // Chained setTimeout instead of setInterval — self-scheduling means throttling
          // slows the cadence but can never fully stop the chain. If we see NO heartbeats
          // after the immediate one, we know the JS context is truly gone or timers were
          // suspended (rather than throttled).
          let heartbeatChainAlive = true;
          async function heartbeatLoop() {
            while (heartbeatChainAlive) {
              await new Promise((resolve) => window.setTimeout(resolve, heartbeatMs));
              if (!heartbeatChainAlive) return;
              sendHeartbeat();
            }
          }
          const heartbeatHandle = 0; // legacy handle name, unused now
          sendHeartbeat();
          heartbeatLoop();

          connectStream();
          startPolling();
          pollFrame();

          // Explicit teardown only when the page is entering BFCache (event.persisted).
          // For real unload the browser cleans up everything anyway, and firing teardown
          // on every pagehide caused silent-death: some WebKit versions fire pagehide
          // spuriously right after page load, killing our setInterval and leaving the
          // canvas blank forever. Cache-Control: no-store on the response is what
          // actually prevents BFCache; this handler is belt-and-braces for the case
          // where the header is ignored.
          function teardown(reason) {
            auditMarker('teardown', reason || 'unknown');
            heartbeatChainAlive = false;
            try { if (pollTimer) window.clearInterval(pollTimer); pollTimer = 0; } catch (_) {}
            try { if (reconnectTimer) window.clearTimeout(reconnectTimer); reconnectTimer = 0; } catch (_) {}
            try {
              if (ws && ws.readyState !== WebSocket.CLOSED && ws.readyState !== WebSocket.CLOSING) {
                ws.onopen = ws.onmessage = ws.onerror = ws.onclose = null;
                ws.close();
              }
            } catch (_) {}
          }
          window.addEventListener('pagehide', (event) => {
            auditMarker('pagehide', event && event.persisted ? 'persisted' : 'discarded', {
              persisted: String(!!(event && event.persisted)),
            });
            if (event && event.persisted) teardown('pagehide_persisted');
          });
        </script>
        </body>
        </html>
        """;
}
