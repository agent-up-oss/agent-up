import { spawn } from 'node:child_process';
import { mkdtemp, mkdir, rm } from 'node:fs/promises';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { createIdpControl } from './idpControl.mjs';
import { writeShims } from './shims.mjs';
import { hostOriginFor, profilesFor, serverEnvironment } from './stackConfig.mjs';
import { waitFor, waitForHttpOk } from './wait.mjs';

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
      child.kill('SIGTERM');
    }
    await rm(root, { recursive: true, force: true });
  };

  try {
    const idpPort = await freePort();
    const idpPublicOrigin = hostOriginFor(platform, idpPort);
    const idp = spawn(join(binDir, 'test-idp'), ['--port', String(idpPort), '--public-origin', idpPublicOrigin], {
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    processes.push(idp);
    idp.stderr.on('data', chunk => process.stderr.write(`[test-idp] ${chunk}`));

    const idpUrl = `http://localhost:${idpPort}`;
    const control = createIdpControl(idpUrl);
    await waitFor('the test identity provider to answer', () => control.health());

    const serverPort = await freePort();
    const serverUrl = `http://localhost:${serverPort}`;
    const server = spawn('dotnet', [serverDll], {
      env: {
        ...process.env,
        PATH: `${binDir}:${process.env.PATH ?? ''}`,
        ...serverEnvironment({
          profiles,
          binDir,
          idpUrl,
          publicOrigin: idpPublicOrigin,
          dataDir,
          // Bound on every interface so an Android emulator can reach it as 10.0.2.2.
          urls: `http://0.0.0.0:${serverPort}`,
        }),
      },
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    processes.push(server);
    server.stdout.on('data', chunk => process.stdout.write(`[server] ${chunk}`));
    server.stderr.on('data', chunk => process.stderr.write(`[server] ${chunk}`));

    await waitForHttpOk('the Agent-Up Server to answer', `${serverUrl}/api/workspaces`);

    const workspace = await registerWorkspace(serverUrl, worktree);

    return {
      serverUrl,
      /** What a client on this platform should be pointed at. */
      serverOriginForClient: hostOriginFor(platform, serverPort),
      idpUrl,
      idpOriginForClient: idpPublicOrigin,
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

function isFree(port) {
  return new Promise(resolve => {
    const probe = createServer();
    probe.once('error', () => resolve(false));
    probe.listen(port, '127.0.0.1', () => probe.close(() => resolve(true)));
  });
}
