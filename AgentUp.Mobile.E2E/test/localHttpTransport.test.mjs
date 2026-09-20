import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import test from 'node:test';

const require = createRequire(import.meta.url);
const {
  allowAndroidCleartext,
  allowIosLocalHttp,
  ANDROID_CLEARTEXT_NETWORK_SECURITY_CONFIG,
  writeAndroidCleartextNetworkSecurityConfig,
} = require('../../AgentUp.Mobile.E2E.App/plugins/withLocalHttpTransport');

test('the Android harness can reach ephemeral HTTP services on the CI host', () => {
  const manifest = { manifest: { application: [{ $: {} }] } };
  allowAndroidCleartext(manifest);
  const application = manifest.manifest.application[0].$;
  assert.equal(application['android:usesCleartextTraffic'], 'true');
  assert.equal(application['android:networkSecurityConfig'], '@xml/network_security_config');
});

test('the Android harness network security config permits the emulator host and loopback', () => {
  assert.match(ANDROID_CLEARTEXT_NETWORK_SECURITY_CONFIG, /cleartextTrafficPermitted="true"/);
  assert.match(ANDROID_CLEARTEXT_NETWORK_SECURITY_CONFIG, />10\.0\.2\.2</);
  assert.match(ANDROID_CLEARTEXT_NETWORK_SECURITY_CONFIG, />localhost</);
  assert.match(ANDROID_CLEARTEXT_NETWORK_SECURITY_CONFIG, />127\.0\.0\.1</);
});

test('prebuild writes the Android cleartext network security config into the native project', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'agent-up-cleartext-'));
  try {
    const dest = writeAndroidCleartextNetworkSecurityConfig(root);
    assert.equal(
      dest,
      path.join(root, 'app', 'src', 'main', 'res', 'xml', 'network_security_config.xml'),
    );
    assert.equal(fs.readFileSync(dest, 'utf8'), ANDROID_CLEARTEXT_NETWORK_SECURITY_CONFIG);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('the iOS harness can reach ephemeral HTTP services on the CI host', () => {
  const infoPlist = { NSAppTransportSecurity: { ExistingSetting: true } };
  allowIosLocalHttp(infoPlist);
  assert.deepEqual(infoPlist.NSAppTransportSecurity, {
    ExistingSetting: true,
    NSAllowsArbitraryLoads: true,
    NSAllowsLocalNetworking: true,
  });
});
