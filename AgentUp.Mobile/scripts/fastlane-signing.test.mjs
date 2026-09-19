import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const require = createRequire(import.meta.url);
const { iosBundleId, androidPackageName } = require('./mobile-expo-config.js');
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

test('Play upload uses the same Android package Expo bakes into the AAB', () => {
  const appfile = readFileSync(join(repoRoot, 'fastlane/Appfile'), 'utf8');
  const mobileCi = readFileSync(join(repoRoot, '.github/workflows/mobile-ci.yaml'), 'utf8');
  assert.match(appfile, new RegExp(`ANDROID_PACKAGE_NAME", "${androidPackageName}"`));
  assert.match(mobileCi, new RegExp(`ANDROID_PACKAGE_NAME: ${androidPackageName}`));
  assert.match(readFileSync(fastfilePath, 'utf8'), new RegExp(`ANDROID_PACKAGE = "${androidPackageName}"`));
});

test('Match and iOS CI use the same bundle id Expo bakes into the IPA', () => {
  const appfile = readFileSync(join(repoRoot, 'fastlane/Appfile'), 'utf8');
  const matchfile = readFileSync(join(repoRoot, 'fastlane/Matchfile'), 'utf8');
  const mobileCi = readFileSync(join(repoRoot, '.github/workflows/mobile-ci.yaml'), 'utf8');
  const certs = readFileSync(join(repoRoot, '.github/workflows/mobile-ios-certs.yaml'), 'utf8');
  assert.match(appfile, new RegExp(`IOS_BUNDLE_ID", "${iosBundleId}"`));
  assert.match(matchfile, new RegExp(`"${iosBundleId}"`));
  assert.match(mobileCi, new RegExp(`IOS_BUNDLE_ID: ${iosBundleId}`));
  assert.match(certs, new RegExp(`IOS_BUNDLE_ID: ${iosBundleId}`));
});
