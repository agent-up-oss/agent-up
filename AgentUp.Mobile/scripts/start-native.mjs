import { spawnSync } from 'node:child_process';
import { resolveExpoCli } from './expo-cli.mjs';

const result = spawnSync(resolveExpoCli(), ['start', ...process.argv.slice(2), '--lan'], {
  env: {
    ...process.env,
    EXPO_PUBLIC_RECOMMENDED_SERVER_URL:
      process.env.EXPO_PUBLIC_RECOMMENDED_SERVER_URL ?? process.env.AGENTUP_RECOMMENDED_SERVER_URL,
    EXPO_PUBLIC_RECOMMENDED_SERVER_NAME:
      process.env.EXPO_PUBLIC_RECOMMENDED_SERVER_NAME ?? process.env.AGENTUP_RECOMMENDED_SERVER_NAME,
  },
  stdio: 'inherit',
  shell: false,
});

if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
