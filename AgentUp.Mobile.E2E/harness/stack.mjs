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
 * An ephemeral port the OS just handed back.
 *
 * Fixed ports collide with whatever else a CI runner is doing, and a collision looks exactly like
 * a flaky test, so nothing in this suite hardcodes one.
 */
export function freePort() {
  return new Promise((resolve, reject) => {
    const probe = createServer();
    probe.on('error', reject);
    probe.listen(0, '127.0.0.1', () => {
      const { port } = probe.address();
      probe.close(() => resolve(port));
    });
  });
}
