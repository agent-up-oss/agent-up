import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const script = fileURLToPath(new URL('./install-android-emulator.sh', import.meta.url));
const workflow = fileURLToPath(new URL('../workflows/mobile-agent-auth-ci.yml', import.meta.url));
const systemImage = 'system-images;android-35;google_apis;x86_64';

function read(path) {
  return readFileSync(path, 'utf8');
}

function runInstaller(home, extraEnv = {}) {
  return spawnSync('bash', [script], {
    encoding: 'utf8',
    env: {
      ...process.env,
      ANDROID_HOME: home,
      AGENTUP_SDK_RETRY_SLEEP: '0',
      ...extraEnv,
    },
  });
}

function writeSdkmanager(home, body) {
  const bin = join(home, 'cmdline-tools', 'latest', 'bin');
  mkdirSync(bin, { recursive: true });
  const path = join(bin, 'sdkmanager');
  writeFileSync(path, `#!/usr/bin/env bash\nset -euo pipefail\n${body}`);
  chmodSync(path, 0o755);
  return path;
}

test('the installer and the Android E2E workflow name the same system image', () => {
  const installer = read(script);
  const yaml = read(workflow);
  assert.match(installer, new RegExp(systemImage.replaceAll(';', '\\;')));
  assert.match(yaml, /api-level:\s*35/);
  assert.match(yaml, /target:\s*google_apis/);
  assert.match(yaml, /arch:\s*x86_64/);
  assert.match(yaml, /install-android-emulator\.sh/);
});

test('leaves the action to install when sdkmanager is missing', () => {
  const home = mkdtempSync(join(tmpdir(), 'agent-up-sdk-'));
  const result = runInstaller(home);
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /No sdkmanager/);
});

test('retries a truncated system-image zip and then succeeds', () => {
  const home = mkdtempSync(join(tmpdir(), 'agent-up-sdk-'));
  const log = join(home, 'sdkmanager.log');
  writeSdkmanager(home, `
package=""
for arg in "$@"; do
  case "$arg" in
    --install) ;;
    --channel=0) ;;
    *) package="$arg" ;;
  esac
done
echo "$package" >> "$ANDROID_HOME/sdkmanager.log"
attempts="$(grep -c "^$package\$" "$ANDROID_HOME/sdkmanager.log" || true)"
if [ "$package" = "emulator" ]; then
  mkdir -p "$ANDROID_HOME/emulator"
  exit 0
fi
if [ "$attempts" -lt 3 ]; then
  echo "Warning: An error occurred while preparing SDK package Google APIs Intel x86_64 Atom System Image: Error on ZipFile unknown archive." >&2
  exit 1
fi
mkdir -p "$ANDROID_HOME/system-images/android-35/google_apis/x86_64"
`);
  const result = runInstaller(home);
  assert.equal(result.status, 0, result.stderr + result.stdout);
  const packages = readFileSync(log, 'utf8').trim().split('\n');
  assert.deepEqual(packages, ['emulator', systemImage, systemImage, systemImage]);
  assert.match(result.stdout, /Installed emulator/);
  assert.match(result.stdout, new RegExp(`Installed ${systemImage.replaceAll(';', '\\;')}`));
});

test('treats an unknown-archive warning as a failed install even when sdkmanager exits 0', () => {
  const home = mkdtempSync(join(tmpdir(), 'agent-up-sdk-'));
  writeSdkmanager(home, `
echo "Warning: An error occurred while preparing SDK package Android Emulator: Error on ZipFile unknown archive." >&2
mkdir -p "$ANDROID_HOME/emulator"
exit 0
`);
  const result = runInstaller(home);
  assert.equal(result.status, 1);
  assert.match(result.stderr, /Could not install emulator after three attempts/);
});

test('fails after three unsuccessful installs', () => {
  const home = mkdtempSync(join(tmpdir(), 'agent-up-sdk-'));
  writeSdkmanager(home, 'echo failed >&2; exit 1');
  const result = runInstaller(home);
  assert.equal(result.status, 1);
  assert.match(result.stderr, /Could not install emulator after three attempts/);
});
