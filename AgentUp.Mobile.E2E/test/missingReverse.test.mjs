import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const { isMissingReverse, tolerateMissingReverse } = require('../detox/missingReverse');

const missing = Object.assign(
  new Error('Command failed: "adb" -s emulator-5554 reverse --remove tcp:36269\nadb: error: listener \'tcp:36269\' not found'),
  { stderr: "adb: error: listener 'tcp:36269' not found" },
);

function adbAnswering(error) {
  return class {
    async reverseRemove() {
      if (error) throw error;
      return 'removed';
    }
  };
}

test('a reverse that is already gone reads as missing, other adb failures do not', () => {
  assert.equal(isMissingReverse(missing), true);
  assert.equal(isMissingReverse(new Error('adb: device offline')), false);
  assert.equal(isMissingReverse(undefined), false);
});

test('removing a reverse that is already gone resolves', async () => {
  const Adb = adbAnswering(missing);
  tolerateMissingReverse(Adb.prototype);
  assert.equal(await new Adb().reverseRemove('emulator-5554', 36269), undefined);
});

test('any other reverse removal failure still throws', async () => {
  const Adb = adbAnswering(new Error('adb: device offline'));
  tolerateMissingReverse(Adb.prototype);
  await assert.rejects(new Adb().reverseRemove('emulator-5554', 36269), /device offline/);
});

test('a successful removal passes through, and patching twice wraps once', async () => {
  const Adb = adbAnswering(null);
  tolerateMissingReverse(Adb.prototype);
  const wrapped = Adb.prototype.reverseRemove;
  tolerateMissingReverse(Adb.prototype);
  assert.equal(Adb.prototype.reverseRemove, wrapped);
  assert.equal(await new Adb().reverseRemove('emulator-5554', 24001), 'removed');
});

test('the Jest config runs Detox through that environment', async () => {
  const config = require('../detox/jest.config.js');
  assert.equal(config.testEnvironment, '<rootDir>/detox/environment.js');
  const environment = await readFile(new URL('../detox/environment.js', import.meta.url), 'utf8');
  assert.match(environment, /tolerateMissingReverse\(ADB\.prototype\)/);
  assert.match(environment, /require\('detox\/runners\/jest\/testEnvironment'\)/);
});

test('every case re-establishes the host reverses before it launches the app', async () => {
  const source = await readFile(new URL('../detox/signIn.test.js', import.meta.url), 'utf8');
  const beforeEach = source.slice(source.indexOf('beforeEach('), source.indexOf("it('signs the agent in"));
  const reverse = beforeEach.indexOf('await reverseHostPorts(stack.hostPorts)');
  assert.ok(reverse > 0, 'beforeEach must reverse the host ports');
  assert.ok(reverse < beforeEach.indexOf('device.launchApp'), 'the reverse must come before launchApp');
});
