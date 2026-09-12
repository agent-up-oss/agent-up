import { spawnSync } from 'node:child_process';
import { existsSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolveExpoCli } from './expo-cli.mjs';

export const distDirectory = 'dist';

export function cleanDist(directory = distDirectory) {
  if (existsSync(directory)) {
    rmSync(directory, { recursive: true, force: true });
  }
}

function isCliEntry() {
  return process.argv[1] === fileURLToPath(import.meta.url);
}

if (isCliEntry()) {
  console.log('Exporting AgentUp.Mobile web build.');
  cleanDist();
  const result = spawnSync(resolveExpoCli(), ['export', '--platform', 'web'], {
    stdio: 'inherit',
    env: {
      ...process.env,
      EXPO_PUBLIC_AGENT_UP_WORKSPACE_ID: process.env.AGENT_UP_WORKSPACE_ID,
      EXPO_PUBLIC_AGENT_UP_APPLICATION: process.env.AGENT_UP_APPLICATION,
      EXPO_PUBLIC_AGENT_UP_AUDIT_ENDPOINT: process.env.AGENT_UP_AUDIT_ENDPOINT,
    },
  });

  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
}
