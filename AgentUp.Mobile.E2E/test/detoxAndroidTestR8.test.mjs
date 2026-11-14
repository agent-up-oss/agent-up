import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import test from 'node:test';

const require = createRequire(import.meta.url);
const {
  ANDROID_TEST_R8_RULES,
  ANDROID_TEST_R8_RULES_FILE,
  TEST_PROGUARD_FILES,
  applyTestProguardFiles,
  writeAndroidTestR8Rules,
} = require('../../AgentUp.Mobile.E2E.App/plugins/withDetoxAndroidTestR8');

const RELEASE_GRADLE = `
    buildTypes {
        debug {
            signingConfig signingConfigs.debug
        }
        release {
            signingConfig signingConfigs.debug
            minifyEnabled enableMinifyInReleaseBuilds
            proguardFiles getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro"
            crunchPngs true
        }
    }
`;

test('androidTest R8 is told the removed RN idle listener may be absent', () => {
  assert.match(
    ANDROID_TEST_R8_RULES,
    /^-dontwarn com\.facebook\.react\.bridge\.NotThreadSafeBridgeIdleDebugListener$/m,
    'R8 must be told the class is gone; -keep cannot resurrect it.',
  );
  assert.equal(
    ANDROID_TEST_R8_RULES.includes('-keep com.facebook.react.bridge.NotThreadSafeBridgeIdleDebugListener'),
    false,
    '-keep on a missing class still fails minifyReleaseAndroidTestWithR8.',
  );
});

test('the release build type points R8 at those androidTest rules', () => {
  const rewritten = applyTestProguardFiles(RELEASE_GRADLE);
  assert.ok(
    rewritten.includes(TEST_PROGUARD_FILES),
    'testProguardFiles has to sit next to proguardFiles so minifyReleaseAndroidTestWithR8 reads it.',
  );
  assert.match(
    rewritten,
    /proguardFiles getDefaultProguardFile\("proguard-android-optimize\.txt"\), "proguard-rules\.pro"\n\s+testProguardFiles "proguard-rules-androidTest\.pro"/,
    'testProguardFiles has to sit next to proguardFiles so minifyReleaseAndroidTestWithR8 reads it.',
  );
  assert.equal(
    applyTestProguardFiles(rewritten),
    rewritten,
    'A second prebuild pass must not duplicate the testProguardFiles line.',
  );
});

test('a build.gradle without proguardFiles is a hard failure, not a silent skip', () => {
  assert.throws(
    () => applyTestProguardFiles('android {\n}\n'),
    /proguardFiles/,
  );
});

test('prebuild writes the androidTest R8 rules next to the app ProGuard file', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'agent-up-androidtest-r8-'));
  try {
    const dest = writeAndroidTestR8Rules(root);
    assert.equal(dest, path.join(root, 'app', ANDROID_TEST_R8_RULES_FILE));
    assert.equal(fs.readFileSync(dest, 'utf8'), ANDROID_TEST_R8_RULES);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
