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

export async function waitForPortAvailable(port, options = {}) {
  const {
    host = '127.0.0.1',
    maxAttempts = 60,
    delayMs = 1000,
    log = console.log,
  } = options;

  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    if (!(await isPortInUse(port, host))) {
      return;
    }

    log(`[agent-up] waiting for WEB_PORT ${port} to become available (attempt ${attempt}/${maxAttempts})`);
    await new Promise((resolve) => setTimeout(resolve, delayMs));
  }

  throw new Error(`WEB_PORT ${port} is still in use after ${maxAttempts} attempts.`);
}
