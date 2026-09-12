import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { mkdtempSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { resolveExpoCli } from './expo-cli.mjs';

test('resolves the local Expo CLI instead of a global expo binary', () => {
  const root = mkdtempSync(join(tmpdir(), 'agent-up-expo-cli-'));
  mkdirSync(join(root, 'node_modules', '.bin'), { recursive: true });
  const cli = join(root, 'node_modules', '.bin', process.platform === 'win32' ? 'expo.cmd' : 'expo');
  writeFileSync(cli, '');

  assert.equal(resolveExpoCli(root), cli);
});

test('fails clearly when Expo has not been installed locally', () => {
  const root = mkdtempSync(join(tmpdir(), 'agent-up-expo-missing-'));
  assert.throws(() => resolveExpoCli(root), /npm ci in AgentUp.Mobile/);
});

test('start, android, and ios invoke Expo through nix-shell and the local CLI helper', async () => {
  const packageJson = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  for (const script of ['start', 'android', 'ios']) {
    assert.match(packageJson.scripts[script], /nix-shell \.\.\/shell\.nix --run '/);
    assert.match(packageJson.scripts[script], /node scripts\/start-native\.mjs/);
    assert.doesNotMatch(packageJson.scripts[script], /(?:^|')expo /);
  }

  const startWeb = await readFile(new URL('./start-web.mjs', import.meta.url), 'utf8');
  assert.match(startWeb, /resolveExpoCli\(\)/);
  assert.match(packageJson.scripts.test, /npx tsc /);
  assert.match(packageJson.scripts.typecheck, /npx tsc /);
});
