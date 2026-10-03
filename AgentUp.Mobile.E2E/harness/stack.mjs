import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { mkdtemp, mkdir, rm } from 'node:fs/promises';
import { createConnection } from 'node:net';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { createIdpControl } from './idpControl.mjs';
import { supervise } from './supervise.mjs';
import { writeShims } from './shims.mjs';
import { hostOriginFor, hostPortsToReverse, profilesFor, serverEnvironment } from './stackConfig.mjs';


/**
 * Brings up a real Agent-Up stack for one end-to-end run: the real Server process, the real test
 * agent CLIs on a PATH the Server resolves them from, and the real identity provider behind them.
 * Nothing here is in-process or faked.
 */
export async function startStack({ platform, codexSchema, serverDll, testAgentExecutable }) {
  const profiles = profilesFor(codexSchema);
  const root = await mkdtemp(join(tmpdir(), 'agent-up-e2e-'));
  const binDir = await writeShims(join(root, 'bin'), testAgentExecutable);
  const dataDir = join(root, 'data');
  const worktree = join(root, 'worktree');
  await mkdir(dataDir, { recursive: true });
  await mkdir(worktree, { recursive: true });

  const processes = [];
  const dispose = async () => {
    for (const child of processes.reverse()) {
      await stopProcess(child);
    }
    await rm(root, { recursive: true, force: true });
  };

  try {
    const idp = await startIdentityProvider(platform, binDir);
    processes.push(idp.child);

    const { idpPort, idpPublicOrigin, control } = idp;
    const idpUrl = `http://localhost:${idpPort}`;

    const serverPort = await freePort();
    const serverUrl = `http://localhost:${serverPort}`;
    const server = supervise('server', spawn('dotnet', [serverDll], {
      env: {
        ...process.env,
        PATH: `${binDir}:${process.env.PATH ?? ''}`,
        ...serverEnvironment({
          profiles,
          binDir,
          idpUrl,
          publicOrigin: idpPublicOrigin,
          dataDir,
          // Bound on every interface so adb reverse from the emulator can reach host loopback.
          urls: `http://0.0.0.0:${serverPort}`,
        }),
      },
      stdio: ['ignore', 'pipe', 'pipe'],
    }));
    processes.push(server.child);

    await server.answers('the Agent-Up Server to answer', async () => {
      const response = await fetch(`${serverUrl}/api/workspaces`);
      return response.ok ? response : false;
    });

    const workspace = await registerWorkspace(serverUrl, worktree);

    return {
      serverUrl,
      serverPort,
      /** What a client on this platform should be pointed at. */
      serverOriginForClient: hostOriginFor(platform, serverPort),
      idpUrl,
      idpPort,
      idpOriginForClient: idpPublicOrigin,
      /** Server and identity-provider ports Android must reverse onto the host. */
      hostPorts: hostPortsToReverse(serverPort, idpPort),
      control,
      workspace,
      profiles,
      dispose,
    };
  } catch (cause) {
    await dispose();
    throw cause;
  }
}

/**
 * Stops a stack process and waits until its handles are closed before its data directory is
 * removed. Sending SIGTERM without waiting let the Server's Chromium profile writer race rm(),
 * which intermittently left a new file in the directory while Node was removing it.
 */
export async function stopProcess(child) {
  if (child.exitCode !== null || child.signalCode !== null) return;

  const closed = once(child, 'close');
  child.kill('SIGTERM');
  await closed;
}

async function registerWorkspace(serverUrl, worktree) {
  const response = await fetch(`${serverUrl}/api/workspaces`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      displayName: 'Agent sign-in end to end',
      repositoryPath: worktree,
      worktreePath: worktree,
      branch: 'main',
      commit: '0000000000000000000000000000000000000000',
    }),
  });

  if (!response.ok) {
    throw new Error(`Could not register the end-to-end workspace: ${response.status} ${await response.text()}`);
  }

  return response.json();
}

/**
 * Ports for one stack.
 *
 * Asking the OS for port 0 and closing the probe leaves a window in which something else can take
 * the port, and scenarios now run side by side, so that window is a collision waiting to happen -
 * which would read as a flaky test rather than as what it is. Instead each worker owns a disjoint
 * window of ports and walks it, never handing out the same one twice and never reaching into
 * another worker's window.
 */
const WINDOW = 4000;
const WORKERS = 8;
const BASE =
  20_000 +
  (Number(process.env.TEST_PARALLEL_INDEX ?? process.env.JEST_WORKER_ID ?? 0) % WORKERS || 0) * WINDOW;

let offset = 0;

export async function freePort() {
  for (let attempt = 0; attempt < WINDOW; attempt++) {
    const port = BASE + (offset++ % WINDOW);
    if (await isFree(port)) return port;
  }

  throw new Error(`No port between ${BASE} and ${BASE + WINDOW} was free.`);
}

/** The window this worker hands ports out of, so a test can assert workers cannot overlap. */
export const portWindow = { base: BASE, size: WINDOW };

/**
 * Starts the identity provider on a port from this worker's window.
 *
 * A named port is not negotiable once the child is told to bind it, so a bind failure has to
 * cost another port rather than the run. Installable-web workers used to die on TIME_WAIT from
 * the listen-and-close probe: HttpListener then reported "Nothing could be bound on port 24000".
 */
async function startIdentityProvider(platform, binDir) {
  let lastError;
  for (let attempt = 0; attempt < 8; attempt++) {
    const idpPort = await freePort();
    const idpPublicOrigin = hostOriginFor(platform, idpPort);
    const idp = supervise('test-idp', spawn(
      join(binDir, 'test-idp'),
      ['--port', String(idpPort), '--public-origin', idpPublicOrigin],
      { stdio: ['ignore', 'pipe', 'pipe'] },
    ));
    const control = createIdpControl(`http://localhost:${idpPort}`);
    try {
      await idp.answers('the test identity provider to answer', () => control.health());
      return { child: idp.child, idpPort, idpPublicOrigin, control };
    } catch (cause) {
      lastError = cause;
      await stopProcess(idp.child);
      if (!idp.ended) throw cause;
    }
  }

  throw lastError;
}

/**
 * True when nothing is accepting on the port.
 *
 * Binding and closing a probe leaves TIME_WAIT, and the identity provider's HttpListener then
 * fails with address already in use. Connecting avoids occupying the port: ECONNREFUSED means it
 * is free, and an accepted connection means it is not.
 */
export function isFree(port) {
  return new Promise(resolve => {
    const socket = createConnection({ port, host: '127.0.0.1' });
    socket.once('connect', () => {
      socket.destroy();
      resolve(false);
    });
    socket.once('error', () => {
      socket.destroy();
      resolve(true);
    });
  });
}
