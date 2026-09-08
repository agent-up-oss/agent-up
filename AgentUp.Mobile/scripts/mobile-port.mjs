import net from 'node:net';

export function resolveMobilePort(value) {
  if (value === undefined || value === '') return 8081;
  if (!/^\d+$/.test(value)) throw new Error('WEB_PORT must be a numeric TCP port.');

  const port = Number(value);
  if (port < 1 || port > 65535) throw new Error('WEB_PORT must be between 1 and 65535.');
  return port;
}

export function isPortInUse(port, host = '127.0.0.1') {
  return new Promise((resolve) => {
    const socket = net.createConnection({ port, host });
    const finish = (inUse) => {
      socket.removeAllListeners();
      socket.destroy();
      resolve(inUse);
    };

    socket.setTimeout(500);
    socket.once('connect', () => finish(true));
    socket.once('timeout', () => finish(false));
    socket.once('error', (error) => {
      finish(error.code !== 'ECONNREFUSED' && error.code !== 'EHOSTUNREACH');
    });
  });
}

export async function ensurePortAvailable(port, options = {}) {
  const {
    host = '127.0.0.1',
    maxGraceAttempts = 5,
    graceDelayMs = 200,
    log = console.log,
  } = options;

  for (let attempt = 1; attempt <= maxGraceAttempts; attempt += 1) {
    if (!(await isPortInUse(port, host))) {
      return;
    }

    if (attempt < maxGraceAttempts) {
      log(`[agent-up] waiting for WEB_PORT ${port} to become available (attempt ${attempt}/${maxGraceAttempts})`);
      await delay(graceDelayMs);
    }
  }

  if (await isPortInUse(port, host)) {
    throw new Error(
      `WEB_PORT ${port} is already in use. Another workspace application may be bound to this port, or a previous dev server did not exit.`,
    );
  }
}

/** @deprecated Use ensurePortAvailable instead. */
export async function waitForPortAvailable(port, options = {}) {
  const {
    maxAttempts,
    delayMs,
    maxGraceAttempts,
    graceDelayMs,
    ...rest
  } = options;

  return ensurePortAvailable(port, {
    ...rest,
    maxGraceAttempts: maxGraceAttempts ?? maxAttempts ?? 5,
    graceDelayMs: graceDelayMs ?? delayMs ?? 200,
  });
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
