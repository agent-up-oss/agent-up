import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const require = createRequire(import.meta.url);
const { canvasColor, createMobileExpoConfig, iosBundleId, androidPackageName } = require('./mobile-expo-config.js');
const { agentUpTheme } = require('../../AgentUp.DesignSystem/dist/web/tokens.cjs');
const appJson = require('../app.json');

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const mobileRoot = join(scriptDirectory, '..');

function pngSize(path) {
  const bytes = readFileSync(path);
  assert.equal(bytes.subarray(0, 8).toString('binary'), '\x89PNG\r\n\x1a\n');
  return {
    width: bytes.readUInt32BE(16),
    height: bytes.readUInt32BE(20),
  };
}

test('uses the design-system canvas token and the themassiveone store ids', () => {
  assert.equal(iosBundleId, 'net.themassiveone.agent-up.ios');
  assert.equal(androidPackageName, 'net.themassiveone.agentup.android');
  assert.equal(canvasColor, agentUpTheme.colors.canvas);
});

test('keeps local app.json version and a local version code of 1', () => {
  const config = createMobileExpoConfig(appJson, {});

  assert.equal(config.expo.version, '0.1.0');
  assert.equal(config.expo.ios.bundleIdentifier, iosBundleId);
  assert.equal(config.expo.ios.buildNumber, '1');
  assert.equal(config.expo.ios.infoPlist.ITSAppUsesNonExemptEncryption, false);
  assert.equal(config.expo.android.package, androidPackageName);
  assert.equal(config.expo.android.versionCode, 1);
  assert.equal(config.expo.android.adaptiveIcon.backgroundColor, canvasColor);
  assert.equal(config.expo.splash.backgroundColor, canvasColor);
  assert.equal(config.expo.web.bundler, 'metro');
});

test('overlays CI marketing version and store build number', () => {
  const config = createMobileExpoConfig(appJson, {
    AGENTUP_MOBILE_VERSION: '2.3.4',
    AGENTUP_MOBILE_VERSION_CODE: '99',
  });

  assert.equal(config.expo.version, '2.3.4');
  assert.equal(config.expo.ios.buildNumber, '99');
  assert.equal(config.expo.android.versionCode, 99);
});

test('rejects a non-semver marketing version and a non-positive version code', () => {
  assert.throws(
    () => createMobileExpoConfig(appJson, { AGENTUP_MOBILE_VERSION: 'android-v1.2.3' }),
    /not X\.Y\.Z/,
  );
  assert.throws(
    () => createMobileExpoConfig(appJson, { AGENTUP_MOBILE_VERSION_CODE: '0' }),
    /positive integer/,
  );
  assert.throws(
    () => createMobileExpoConfig(appJson, { AGENTUP_MOBILE_VERSION_CODE: '01' }),
    /positive integer/,
  );
});

test('commits 1024px store icons scaled from the PWA 512px mark', () => {
  assert.deepEqual(pngSize(join(mobileRoot, 'public', 'agent-up-icon-512.png')), { width: 512, height: 512 });
  assert.deepEqual(pngSize(join(mobileRoot, 'assets', 'icon.png')), { width: 1024, height: 1024 });
  assert.deepEqual(pngSize(join(mobileRoot, 'assets', 'adaptive-icon.png')), { width: 1024, height: 1024 });
});
