import { spawnSync } from 'node:child_process';
import { resolveMobilePort } from './mobile-port.mjs';

const port = resolveMobilePort(process.env.WEB_PORT);
const result = spawnSync('expo', ['start', '--web', '--lan', '--port', String(port)], {
  env: process.env,
  stdio: 'inherit',
  shell: false,
});

if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
