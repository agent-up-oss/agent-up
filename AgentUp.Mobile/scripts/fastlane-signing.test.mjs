import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const mobileRoot = join(dirname(fileURLToPath(import.meta.url)), '..');

test('the Android release gradle call does not print the signing command', () => {
  const fastfile = readFileSync(join(mobileRoot, 'fastlane/Fastfile'), 'utf8');
  const gradleCall = fastfile.match(/gradle\([\s\S]*?android\.injected\.signing[\s\S]*?\n    \)/);
  assert.ok(gradleCall, 'expected a gradle call that injects Android signing properties');
  assert.match(gradleCall[0], /print_command:\s*false/);
});
