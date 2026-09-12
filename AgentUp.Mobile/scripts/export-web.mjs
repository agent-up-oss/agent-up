import { spawnSync } from 'node:child_process';
import { existsSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolveExpoCli } from './expo-cli.mjs';

export const distDirectory = 'dist';

export function webExportEnv(env = process.env) {
  const sentryDsn = env.EXPO_PUBLIC_SENTRY_DSN || env.SENTRY_DSN_MOBILE;
  const sentryRelease = env.EXPO_PUBLIC_SENTRY_RELEASE;
  return {
    ...env,
    EXPO_PUBLIC_AGENT_UP_WORKSPACE_ID: env.AGENT_UP_WORKSPACE_ID,
    EXPO_PUBLIC_AGENT_UP_APPLICATION: env.AGENT_UP_APPLICATION,
    EXPO_PUBLIC_AGENT_UP_AUDIT_ENDPOINT: env.AGENT_UP_AUDIT_ENDPOINT,
    ...(sentryDsn ? { EXPO_PUBLIC_SENTRY_DSN: sentryDsn } : {}),
    ...(sentryRelease ? { EXPO_PUBLIC_SENTRY_RELEASE: sentryRelease } : {}),
  };
}

export function cleanDist(directory = distDirectory) {
  if (existsSync(directory)) {
    rmSync(directory, { recursive: true, force: true });
  }
}

function runExport() {
  console.log('Exporting AgentUp.Mobile web build.');
  cleanDist();
  const result = spawnSync(resolveExpoCli(), ['export', '--platform', 'web'], {
    stdio: 'inherit',
    env: webExportEnv(),
  });

  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  runExport();
}
