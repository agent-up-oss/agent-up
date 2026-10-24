import { spawn } from 'node:child_process';
import { mkdir, access } from 'node:fs/promises';
import { mkdtemp } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { startStaticServer } from '../../harness/staticServer.mjs';
import { freePort, removeWorkspaceTree, stopProcess } from '../../harness/stack.mjs';
import { supervise } from '../../harness/supervise.mjs';

const WORKSPACE_NAME = 'Agent-Up';

/**
 * A real Agent-Up Server plus the Mobile web export, isolated to this run.
 *
 * Auth is off so the connect form is the thing under test, not a password wall. State lives in a
 * temp directory so a leftover Development Server on :5001 cannot leak workspaces into the flow.
 */
export async function startValidationHost(repoRoot) {
  const exportDir = join(repoRoot, 'AgentUp.Mobile', 'dist');
  await assertExport(exportDir);

  const root = await mkdtemp(join(tmpdir(), 'agent-up-validate-'));
  const dataDir = join(root, 'data');
  await mkdir(dataDir, { recursive: true });

  const processes = [];
  const dispose = async () => {
    for (const child of processes.reverse())
      await stopProcess(child);
    await site?.dispose();
    await removeWorkspaceTree(root);
  };

  let site;
  try {
    const serverPort = await freePort();
    const serverUrl = `http://127.0.0.1:${serverPort}`;
    const server = supervise('server', spawn('dotnet', [
      'run',
      '--project',
      join(repoRoot, 'AgentUp.Server', 'AgentUp.Server.csproj'),
      '--no-launch-profile',
    ], {
      cwd: repoRoot,
      env: {
        ...process.env,
        ASPNETCORE_URLS: `http://127.0.0.1:${serverPort}`,
        ASPNETCORE_ENVIRONMENT: 'Development',
        AGENTUP_AUTH_DISABLED: 'true',
        AGENTUP_SKIP_DEV_AGENT_BOOTSTRAP: '1',
        Storage__DataDirectory: dataDir,
      },
      stdio: ['ignore', 'pipe', 'pipe'],
    }));
    processes.push(server.child);

    await server.answers('the Agent-Up Server to answer', async () => {
      const response = await fetch(`${serverUrl}/api/workspaces`);
      return response.ok ? response : false;
    }, { timeoutMs: 180_000 });

    const workspace = await registerWorkspace(serverUrl, repoRoot);
    site = await startStaticServer(exportDir);

    return {
      serverUrl,
      mobileUrl: site.url,
      workspace,
      workspaceName: WORKSPACE_NAME,
      dispose,
    };
  } catch (cause) {
    await dispose();
    throw cause;
  }
}

async function registerWorkspace(serverUrl, repoRoot) {
  const response = await fetch(`${serverUrl}/api/workspaces`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      displayName: WORKSPACE_NAME,
      repositoryPath: repoRoot,
      worktreePath: repoRoot,
      branch: 'main',
      commit: '0000000000000000000000000000000000000000',
    }),
  });
  if (!response.ok)
    throw new Error(`Could not register the validation workspace: ${response.status} ${await response.text()}`);
  return response.json();
}

async function assertExport(exportDir) {
  try {
    await access(join(exportDir, 'index.html'));
  } catch {
    throw new Error(`Mobile web export is missing at ${exportDir}. Run: ./au-debug build mobile`);
  }
}

export function varsFor(host) {
  return {
    serverUrl: host.serverUrl,
    workspaceId: host.workspace.id,
    workspaceName: host.workspaceName,
  };
}
