import { spawnSync } from 'node:child_process';
import { resolveMobilePort } from './mobile-port.mjs';

const port = resolveMobilePort(process.env.WEB_PORT);
const result = spawnSync('expo', ['start', '--web', '--lan', '--port', String(port)], {
  env: {
    ...process.env,
    EXPO_PUBLIC_AGENT_UP_WORKSPACE_ID: process.env.AGENT_UP_WORKSPACE_ID,
    EXPO_PUBLIC_AGENT_UP_APPLICATION: process.env.AGENT_UP_APPLICATION,
    EXPO_PUBLIC_AGENT_UP_AUDIT_ENDPOINT: process.env.AGENT_UP_AUDIT_ENDPOINT,
  },
  stdio: 'inherit',
  shell: false,
});

if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
