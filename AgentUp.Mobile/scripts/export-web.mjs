import { spawnSync } from 'node:child_process';
import { existsSync, rmSync } from 'node:fs';
import { join } from 'node:path';

export const distDirectory = 'dist';

export function cleanDist(directory = distDirectory) {
  if (existsSync(directory)) {
    rmSync(directory, { recursive: true, force: true });
  }
}

const executable = process.platform === 'win32' ? 'expo.cmd' : 'expo';
const expo = join('node_modules', '.bin', executable);
if (!existsSync(expo)) {
  throw new Error('Expo is not installed. Run npm ci before exporting the web build.');
}

console.log('Exporting AgentUp.Mobile web build.');
cleanDist();
const result = spawnSync(expo, ['export', '--platform', 'web'], {
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
