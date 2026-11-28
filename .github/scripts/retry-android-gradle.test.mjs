import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const script = fileURLToPath(new URL('./retry-android-gradle.sh', import.meta.url));
const signIn = fileURLToPath(new URL('../actions/sign-in-android/action.yml', import.meta.url));
const store = fileURLToPath(new URL('../actions/mobile-store-android/action.yml', import.meta.url));

function runRetry(home, command, extraEnv = {}) {
  return spawnSync('bash', [script, ...command], {
    encoding: 'utf8',
    env: {
      ...process.env,
      GRADLE_USER_HOME: home,
      AGENTUP_GRADLE_RETRY_SLEEP: '0',
      AGENTUP_GRADLE_RETRY_ATTEMPTS: '3',
      ...extraEnv,
    },
  });
}

function writeCommand(home, body) {
  const path = join(home, 'gradlew');
  writeFileSync(path, `#!/usr/bin/env bash\nset -euo pipefail\n${body}`);
  chmodSync(path, 0o755);
  return path;
}

test('the Android sign-in and store jobs wrap Gradle with the retry script', () => {
  const signInYaml = readFileSync(signIn, 'utf8');
  const storeYaml = readFileSync(store, 'utf8');
  assert.match(signInYaml, /retry-android-gradle\.sh/);
  assert.match(storeYaml, /retry-android-gradle\.sh/);
});

test('succeeds on the first attempt without looking at Gradle caches', () => {
  const home = mkdtempSync(join(tmpdir(), 'agent-up-gradle-'));
  const gradlew = writeCommand(home, 'echo built');
  const result = runRetry(home, [gradlew]);
  assert.equal(result.status, 0, result.stderr + result.stdout);
  assert.match(result.stdout, /attempt 1\/3/);
  assert.equal(result.stdout.includes('attempt 2/'), false);
});

test('retries a plugin-portal miss after dropping the negative cache', () => {
  const home = mkdtempSync(join(tmpdir(), 'agent-up-gradle-'));
  const files = join(home, 'caches', 'modules-2', 'files-2.1');
  const plugin = join(files, 'org.gradle.toolchains.foojay-resolver-convention');
  const keep = join(files, 'com.example.unrelated');
  const metadata = join(home, 'caches', 'modules-2', 'metadata-2.107');
  mkdirSync(plugin, { recursive: true });
  mkdirSync(keep, { recursive: true });
  mkdirSync(metadata, { recursive: true });
  writeFileSync(join(plugin, 'miss'), 'stale');
  writeFileSync(join(keep, 'jar'), 'keep');
  writeFileSync(join(metadata, 'resource-at-url.bin'), 'cached-404');
  const log = join(home, 'attempts.log');
  const gradlew = writeCommand(home, `
echo attempt >> "$GRADLE_USER_HOME/attempts.log"
attempts="$(wc -l < "$GRADLE_USER_HOME/attempts.log" | tr -d ' ')"
if [ "$attempts" -lt 2 ]; then
  echo "Plugin [id: 'org.gradle.toolchains.foojay-resolver-convention', version: '1.0.0'] was not found" >&2
  echo "could not resolve plugin artifact 'org.gradle.toolchains.foojay-resolver-convention:org.gradle.toolchains.foojay-resolver-convention.gradle.plugin:1.0.0'" >&2
  exit 1
fi
if [ -e "$GRADLE_USER_HOME/caches/modules-2/metadata-2.107/resource-at-url.bin" ]; then
  echo "metadata miss cache was still present" >&2
  exit 1
fi
if [ -e "$GRADLE_USER_HOME/caches/modules-2/files-2.1/org.gradle.toolchains.foojay-resolver-convention/miss" ]; then
  echo "plugin marker cache was still present" >&2
  exit 1
fi
if [ ! -e "$GRADLE_USER_HOME/caches/modules-2/files-2.1/com.example.unrelated/jar" ]; then
  echo "unrelated artifact was dropped" >&2
  exit 1
fi
echo built
`);
  const result = runRetry(home, [gradlew]);
  assert.equal(result.status, 0, result.stderr + result.stdout);
  assert.match(result.stdout, /Plugin portal miss/);
  assert.equal(readFileSync(log, 'utf8').trim().split('\n').length, 2);
});

test('does not retry an unrelated Gradle failure', () => {
  const home = mkdtempSync(join(tmpdir(), 'agent-up-gradle-'));
  const log = join(home, 'attempts.log');
  const gradlew = writeCommand(home, `
echo attempt >> "$GRADLE_USER_HOME/attempts.log"
echo "FAILURE: Build failed with an exception." >&2
echo "Compilation failed" >&2
exit 1
`);
  const result = runRetry(home, [gradlew]);
  assert.equal(result.status, 1, result.stderr + result.stdout);
  assert.equal(readFileSync(log, 'utf8').trim().split('\n').length, 1);
  assert.equal(result.stdout.includes('Plugin portal miss'), false);
});
