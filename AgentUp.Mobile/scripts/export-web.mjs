import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveExpoCli } from './expo-cli.mjs';

export const distDirectory = 'dist';
export const clientBundleScriptPattern = /<script[^>]+src=["'][^"']*_expo\/static\/js\//;

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

export function exportedHtmlIncludesClientBundle(html) {
  return clientBundleScriptPattern.test(html);
}

export function assertExportedHtmlHydrates(directory = distDirectory) {
  const html = readFileSync(join(directory, 'index.html'), 'utf8');
  if (!exportedHtmlIncludesClientBundle(html)) {
    throw new Error(
      'Static export HTML is missing the client bundle. src/app/+html.tsx must render bodyNodes from useServerDocumentContext().',
    );
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
  if (result.status) {
    process.exitCode = result.status;
    return;
  }

  assertExportedHtmlHydrates();
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  runExport();
}
