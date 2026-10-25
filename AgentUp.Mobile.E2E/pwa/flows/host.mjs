import { spawn } from 'node:child_process';
import { mkdir, access, mkdtemp, readFile, writeFile, chmod } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { delimiter as pathDelimiter, join } from 'node:path';

import { startStaticServer } from '../../harness/staticServer.mjs';
import { freePort, removeWorkspaceTree, stopProcess } from '../../harness/stack.mjs';
import { supervise } from '../../harness/supervise.mjs';

const WORKSPACE_NAME = 'Agent Up';

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
  const binDir = join(root, 'bin');
  await mkdir(dataDir, { recursive: true });
  const agentEnv = await scriptedAgentEnvironment(repoRoot, binDir);

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
        ...agentEnv,
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
      serverLog: () => server.said(),
      dispose,
    };
  } catch (cause) {
    await dispose();
    throw cause;
  }
}

const RESERVED_CONFIG_KEYS = new Set([
  'name',
  'display',
  'prompts',
  'commits',
  'verification',
  'coverage',
]);

async function registerWorkspace(serverUrl, repoRoot) {
  const config = JSON.parse(await readFile(join(repoRoot, 'agent-up.json'), 'utf8'));
  const body = {
    displayName: WORKSPACE_NAME,
    repositoryPath: repoRoot,
    worktreePath: repoRoot,
    branch: 'main',
    commit: '0000000000000000000000000000000000000000',
  };
  for (const [key, value] of Object.entries(config)) {
    if (RESERVED_CONFIG_KEYS.has(key) || !Array.isArray(value))
      continue;
    body[key] = value;
  }
  const response = await fetch(`${serverUrl}/api/workspaces`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
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

export async function scriptedAgentEnvironment(repoRoot, binDir) {
  const executable = await findTestAgentsExecutable(repoRoot);
  await mkdir(binDir, { recursive: true });
  const scripted = await writeDevelopmentAgentShim(binDir, executable, 'test-agent-scripted');
  const infer = await writeDevelopmentAgentShim(binDir, executable, 'test-agent-infer');
  return {
    PATH: `${binDir}${pathDelimiter}${process.env.PATH ?? ''}`,
    'Agents__test-agent-scripted__Command': scripted,
    'Agents__test-agent-infer__Command': infer,
  };
}

async function writeDevelopmentAgentShim(binDir, executable, agentId) {
  const shim = join(binDir, agentId);
  await writeFile(shim, [
    '#!/bin/sh',
    'set -e',
    `export AGENTUP_TEST_AGENT='${agentId}'`,
    `exec '${executable}' "$@"`,
    '',
  ].join('\n'));
  await chmod(shim, 0o755);
  return shim;
}

async function findTestAgentsExecutable(repoRoot) {
  for (const configuration of ['Debug', 'Release']) {
    const candidate = join(repoRoot, 'AgentUp.TestAgents', 'bin', configuration, 'net10.0', 'AgentUp.TestAgents');
    try {
      await access(candidate);
      return candidate;
    } catch (error) {
      if (error && typeof error === 'object' && 'code' in error && error.code !== 'ENOENT')
        throw error;
    }
  }
  throw new Error('Could not find AgentUp.TestAgents. Build AgentUp.TestAgents before au-debug validate --surface mobile.');
}

export function varsFor(host) {
  return {
    serverUrl: host.serverUrl,
    workspaceId: host.workspace.id,
    workspaceName: host.workspaceName,
  };
}
