// AgentUp RDP viewer — client-side state machine.
//
// One of three cooperating state machines in the viewer pipeline. See the plan at
// mossy-popping-twilight.md for the architecture overview.
//
// State machine:
//   initializing -> connecting -> open_no_frames -> streaming
//                                       ^                 |
//                                       |                 v
//                                   disconnected <-- stalled
//                                       |                 |
//                                       v                 v
//                                   connecting <----- polling
//
// Public surface (window.__viewer):
//   snapshot()               — synchronous read of the current SM state + counters.
//   setPresence(state)       — 'foreground' | 'background'. Forwarded to server via WS.
//   reset()                  — tear down + re-init (Avalonia calls this to force a fresh
//                              subscription without navigating the page).
//
// The state machine is the ONLY writer of `state`. Everything else observes.

(function () {
  'use strict';

  // ─── Config ────────────────────────────────────────────────────────────
  const HEARTBEAT_MS = 3000;
  const STALLED_FRAME_GAP_MS = 3000;
  const POLL_INTERVAL_MS = 250;
  const RECONNECT_BACKOFF_MS = 500;

  // ─── Identity ──────────────────────────────────────────────────────────
  const workspaceId = new URLSearchParams(location.search).get('workspaceId') || '';
  const pageInstanceId =
    Math.random().toString(36).slice(2, 10) +
    Math.random().toString(36).slice(2, 10);
  const pageLoadedAt = Date.now();

  // ─── DOM ───────────────────────────────────────────────────────────────
  const canvas = document.getElementById('c');
  const ctx = canvas.getContext('2d');

  // ─── Connection targets ────────────────────────────────────────────────
  const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
  const streamUrl = `${proto}//${location.host}/api/browser/rdp/${encodeURIComponent(workspaceId)}`;
  const pollUrlBase = `/api/browser/rdp/${encodeURIComponent(workspaceId)}/frame`;

  // ─── Observable properties (returned via snapshot) ─────────────────────
  let state = 'initializing';
  let stateSince = Date.now();
  let framesReceived = 0;
  let lastFrameAt = 0;
  let presence = 'foreground';
  let ws = null;
  let pollTimer = 0;
  let reconnectTimer = 0;
  let serverReportedActive = false;

  // ─── Audit trail (diagnostic only, does not drive lifecycle) ───────────
  function auditMarker(action, outcome, extra) {
    try {
      const details = Object.assign({
        pageInstanceId,
        pageAgeMs: String(Date.now() - pageLoadedAt),
        state,
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

  // ─── State machine core ────────────────────────────────────────────────
  const STATES = new Set([
    'initializing', 'connecting', 'open_no_frames', 'streaming',
    'stalled', 'polling', 'disconnected',
  ]);

  function transitionTo(nextState, reason) {
    if (!STATES.has(nextState)) return;
    if (state === nextState) return;
    const prev = state;
    state = nextState;
    stateSince = Date.now();
    auditMarker('viewer_state_changed', nextState, { from: prev, reason: reason || '' });
    onEnter(nextState, prev);
  }

  function onEnter(next /*, prev */) {
    switch (next) {
      case 'connecting':
        openWebSocket();
        break;
      case 'polling':
        stopReconnectTimer();
        startPolling();
        armReconnect();
        break;
      case 'disconnected':
        stopReconnectTimer();
        armReconnect();
        break;
      case 'streaming':
        // WS is delivering; HTTP polling is redundant burn.
        stopPolling();
        break;
      case 'stalled':
        // WS still open but frames dried up — try the HTTP fallback while we wait.
        startPolling();
        break;
    }
  }

  // ─── WebSocket ─────────────────────────────────────────────────────────
  function openWebSocket() {
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) return;
    try {
      ws = new WebSocket(streamUrl);
    } catch (_) {
      transitionTo('disconnected', 'ws_ctor_threw');
      return;
    }
    ws.binaryType = 'arraybuffer';
    ws.onopen = () => {
      transitionTo('open_no_frames', 'ws_open');
      sendPresence();
    };
    ws.onmessage = (e) => {
      if (typeof e.data === 'string') handleTextFrame(e.data);
      else handleBinaryFrame(e.data);
    };
    ws.onerror = () => {
      // ws.onclose almost always follows; only handle here if we were pre-open.
      if (state === 'connecting') transitionTo('disconnected', 'ws_error_pre_open');
    };
    ws.onclose = () => {
      serverReportedActive = false;
      if (state !== 'disconnected') transitionTo('polling', 'ws_close');
    };
  }

  function handleTextFrame(text) {
    try {
      const msg = JSON.parse(text);
      if (msg && msg.type === 'stream' && (msg.state === 'active' || msg.state === 'paused')) {
        serverReportedActive = (msg.state === 'active');
      }
    } catch (_) {}
  }

  function handleBinaryFrame(data) {
    drawBlob(new Blob([data], { type: 'image/jpeg' }));
  }

  // ─── Rendering ─────────────────────────────────────────────────────────
  function drawBlob(blob) {
    const url = URL.createObjectURL(blob);
    const img = new Image();
    img.onload = () => {
      if (canvas.width !== img.width) canvas.width = img.width;
      if (canvas.height !== img.height) canvas.height = img.height;
      ctx.drawImage(img, 0, 0);
      framesReceived += 1;
      lastFrameAt = Date.now();
      URL.revokeObjectURL(url);
      onFrame();
    };
    img.onerror = () => URL.revokeObjectURL(url);
    img.src = url;
  }

  function onFrame() {
    if (state === 'open_no_frames' || state === 'stalled') {
      transitionTo('streaming', 'first_frame');
    }
  }

  // ─── Polling fallback ──────────────────────────────────────────────────
  async function pollFrame() {
    try {
      const res = await fetch(`${pollUrlBase}?t=${Date.now()}`, { cache: 'no-store' });
      if (!res.ok) return;
      drawBlob(await res.blob());
    } catch (_) {}
  }

  function startPolling() {
    if (pollTimer) return;
    pollTimer = window.setInterval(() => {
      if (!lastFrameAt || Date.now() - lastFrameAt > POLL_INTERVAL_MS) pollFrame();
    }, POLL_INTERVAL_MS);
    pollFrame();
  }

  function stopPolling() {
    if (pollTimer) { window.clearInterval(pollTimer); pollTimer = 0; }
  }

  function armReconnect() {
    if (reconnectTimer) return;
    reconnectTimer = window.setTimeout(() => {
      reconnectTimer = 0;
      transitionTo('connecting', 'reconnect_backoff');
    }, RECONNECT_BACKOFF_MS);
  }

  function stopReconnectTimer() {
    if (reconnectTimer) { window.clearTimeout(reconnectTimer); reconnectTimer = 0; }
  }

  // ─── Stall detection (fires on heartbeat tick) ─────────────────────────
  function checkStall() {
    if (state === 'streaming'
        && lastFrameAt
        && Date.now() - lastFrameAt > STALLED_FRAME_GAP_MS) {
      transitionTo('stalled', 'frame_gap');
    }
  }

  // ─── Presence ──────────────────────────────────────────────────────────
  function sendPresence() {
    if (!ws || ws.readyState !== WebSocket.OPEN) return;
    try {
      ws.send(JSON.stringify({ type: 'presence', state: presence }));
    } catch (_) {}
  }

  function setPresence(next) {
    if (next !== 'foreground' && next !== 'background') return;
    if (next === presence) return;
    presence = next;
    sendPresence();
  }

  // ─── Heartbeat (diagnostic only; not lifecycle-critical) ───────────────
  let heartbeatChainAlive = true;

  async function heartbeatLoop() {
    while (heartbeatChainAlive) {
      await new Promise((resolve) => window.setTimeout(resolve, HEARTBEAT_MS));
      if (!heartbeatChainAlive) return;
      sendHeartbeat();
      checkStall();
    }
  }

  function sendHeartbeat() {
    const now = Date.now();
    const outcome = state === 'streaming' ? 'streaming'
                   : state === 'stalled' ? 'stalled'
                   : 'idle';
    const details = {
      pageInstanceId,
      pageAgeMs: String(now - pageLoadedAt),
      state,
      stateAgeMs: String(now - stateSince),
      framesReceived: String(framesReceived),
      lastFrameAgoMs: lastFrameAt ? String(now - lastFrameAt) : '',
      canvasWidth: String(canvas.width),
      canvasHeight: String(canvas.height),
      wsReadyState: wsStateName(),
      documentVisibilityState: document.visibilityState || 'unknown',
      presence,
      serverReportedActive: String(serverReportedActive),
    };
    fetch('/api/audit/record', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        Kind: 'stream', Source: 'viewer', Action: 'heartbeat',
        Outcome: outcome, WorkspaceId: workspaceId, Details: details,
      }),
      cache: 'no-store', keepalive: true,
    }).catch(() => {});
  }

  function wsStateName() {
    if (!ws) return 'none';
    if (ws.readyState === WebSocket.CONNECTING) return 'connecting';
    if (ws.readyState === WebSocket.OPEN) return 'open';
    if (ws.readyState === WebSocket.CLOSING) return 'closing';
    if (ws.readyState === WebSocket.CLOSED) return 'closed';
    return 'none';
  }

  // ─── Snapshot API — Avalonia's synchronous read ────────────────────────
  window.__viewer = {
    snapshot() {
      return {
        state,
        since: stateSince,
        framesReceived,
        lastFrameAt,
        wsReadyState: wsStateName(),
        presence,
        pageInstanceId,
        serverReportedActive,
      };
    },
    setPresence,
    reset() {
      stopPolling();
      stopReconnectTimer();
      try {
        if (ws && ws.readyState !== WebSocket.CLOSED) {
          ws.onopen = ws.onmessage = ws.onerror = ws.onclose = null;
          ws.close();
        }
      } catch (_) {}
      ws = null;
      serverReportedActive = false;
      framesReceived = 0;
      lastFrameAt = 0;
      transitionTo('initializing', 'external_reset');
      transitionTo('connecting', 'reset_reconnect');
    },
  };

  // ─── Global error hooks (diagnostic) ───────────────────────────────────
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
  window.addEventListener('pagehide', (event) => {
    auditMarker('pagehide', event && event.persisted ? 'persisted' : 'discarded', {
      persisted: String(!!(event && event.persisted)),
    });
    if (event && event.persisted) {
      heartbeatChainAlive = false;
      stopPolling();
      stopReconnectTimer();
    }
  });

  // ─── Boot ──────────────────────────────────────────────────────────────
  sendHeartbeat();
  heartbeatLoop();
  transitionTo('connecting', 'boot');
})();
