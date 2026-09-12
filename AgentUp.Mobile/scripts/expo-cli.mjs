import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const mobileRoot = join(scriptDirectory, '..');

export function resolveExpoCli(root = mobileRoot) {
  const executable = process.platform === 'win32' ? 'expo.cmd' : 'expo';
  const cli = join(root, 'node_modules', '.bin', executable);
  if (!existsSync(cli)) {
    throw new Error('Expo is not installed. Run npm ci in AgentUp.Mobile before starting Expo.');
  }

  return cli;
}
