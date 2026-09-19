import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '../..');
const fastfilePath = join(repoRoot, 'fastlane/Fastfile');

test('the Android release gradle call does not print the signing command', () => {
  const fastfile = readFileSync(fastfilePath, 'utf8');
  const gradleCall = fastfile.match(/gradle\([\s\S]*?android\.injected\.signing[\s\S]*?\n    \)/);
  assert.ok(gradleCall, 'expected a gradle call that injects Android signing properties');
  assert.match(gradleCall[0], /print_command:\s*false/);
});

test('the iOS build locates the Expo workspace from AgentUp.Mobile after Match', () => {
  const fastfile = readFileSync(fastfilePath, 'utf8');
  assert.match(fastfile, /FastlaneCore::FastlaneFolder\.path/);
  assert.match(fastfile, /File\.join\(repo_root, "AgentUp\.Mobile"\)/);
  assert.match(fastfile, /File\.join\(mobile_root, "ios"/);
  assert.doesNotMatch(fastfile, /Dir\.glob\("ios\/\*/);
  const buildApp = fastfile.match(/build_app\([\s\S]*?export_options:/);
  assert.ok(buildApp, 'expected an iOS build_app call');
  assert.match(buildApp[0], /workspace:\s*workspace/);
  assert.doesNotMatch(buildApp[0], /project:\s*project/);
  assert.match(fastfile, /File\.expand_path\(ENV\.fetch\("MOBILE_OUTPUT_DIR", "dist-native"\), mobile_root\)/);
});

test('the Android gradle project is AgentUp.Mobile/android', () => {
  const fastfile = readFileSync(fastfilePath, 'utf8');
  assert.match(fastfile, /project_dir:\s*File\.join\(mobile_root, "android"\)/);
});
