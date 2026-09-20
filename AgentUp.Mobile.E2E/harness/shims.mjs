import { chmod, mkdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

import { AGENT_PROFILES } from './stackConfig.mjs';

export const IDENTITY_PROVIDER_SHIM = 'test-idp';

/**
 * One binary is published for every test agent, so each gets a launcher that names which agent it
 * is. The name is passed explicitly rather than inferred from the executable path: a renamed or
 * symlinked apphost does not reliably report the name it was invoked under, and a wrong guess
 * would silently run the wrong sign-in.
 */
export function shimScript(executable, agentName) {
  return [
    '#!/bin/sh',
    'set -e',
    `AGENTUP_TEST_AGENT='${agentName}'`,
    'export AGENTUP_TEST_AGENT',
    `exec '${executable}' "$@"`,
    '',
  ].join('\n');
}

/** Writes a launcher per test agent plus the identity provider, and returns the directory. */
export async function writeShims(binDir, executable) {
  await mkdir(binDir, { recursive: true });

  const names = [...new Set(Object.values(AGENT_PROFILES).map(profile => profile.agent)), IDENTITY_PROVIDER_SHIM];
  for (const name of names) {
    const path = join(binDir, name);
    await writeFile(path, shimScript(executable, name), 'utf8');
    await chmod(path, 0o755);
  }

  return binDir;
}
