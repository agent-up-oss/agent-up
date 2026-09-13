import { spawnSync } from 'node:child_process';
import { resolveExpoCli } from './expo-cli.mjs';

const result = spawnSync(resolveExpoCli(), ['start', ...process.argv.slice(2), '--lan'], {
  stdio: 'inherit',
  shell: false,
});

if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
