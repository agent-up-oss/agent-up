import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const json = where => JSON.parse(readFileSync(new URL(where, import.meta.url), 'utf8'));

const runner = json('../package.json');
const app = json('../../AgentUp.Mobile.E2E.App/package.json');

// Detox is two halves of one conversation: the runner drives the tests from here, and a native
// Detox compiled into the app under test answers it over a socket. They speak a protocol that is
// versioned with them, so two different versions do not talk to each other - they hang, which on
// a simulator looks like a flaky test rather than a mismatched dependency. Both halves therefore
// pin the same exact version, and this is what says so out loud.
test('the app under test carries the same Detox the runner drives it with', () => {
  const driving = runner.devDependencies.detox;
  const inside = app.devDependencies.detox;

  assert.match(driving, /^\d+\.\d+\.\d+$/, 'The runner must pin Detox exactly, not by range.');
  assert.equal(
    inside,
    driving,
    `AgentUp.Mobile.E2E.App pins Detox ${inside} and this runner pins ${driving}. ` +
      'Whichever is wrong, change it: the native half of Detox is built from the app.',
  );
});

// The version is not only installed, it is written into the Android build: app/build.gradle asks
// for com.wix:detox by that exact number, because com.wix:detox also exists on Maven Central as an
// abandoned 0.1.1 stub that a `+` range resolves to silently.
test('the Android build asks for that Detox by version rather than by range', () => {
  // package.json may carry an expo key - autolinking options are only read from there - but the
  // app config, plugins included, belongs in app.json, which is where this reads it from. A
  // plugins list in both places would leave this test passing on the half nobody builds.
  assert.equal(
    app.expo?.plugins,
    undefined,
    'The plugins list belongs in app.json, not package.json, or this checks the wrong one.',
  );

  const plugins = json('../../AgentUp.Mobile.E2E.App/app.json').expo.plugins;
  assert.ok(
    plugins.includes('./plugins/withExactDetoxVersion'),
    'withExactDetoxVersion is what replaces com.wix:detox:+ with the installed version.',
  );
});

// Removing this reintroduces a crash, not a slowdown, so it is worth a test of its own: without it
// Detox builds its network idling resource at startup, reflects into React Native for a field the
// New Architecture does not have, and takes the app down before any scenario runs. iOS does not
// crash, but waiting for idle during launchApp cancelled getAgent and left the picker empty.
test('the Android gradle command is scoped to the app module', async () => {
  const { default: config } = await import('../.detoxrc.js');
  const build = config.apps['android.release'].build;

  assert.match(
    build,
    /:app:assembleRelease/,
    'assembleRelease must be :app-scoped; the suite installs app-release.apk only.',
  );
  assert.match(
    build,
    /:app:assembleAndroidTest/,
    'assembleAndroidTest must be :app-scoped; unscoped packaging builds every library debug androidTest APK.',
  );
  assert.equal(
    build.includes(' assembleAndroidTest'),
    false,
    'An unscoped assembleAndroidTest reintroduces :expo-constants:packageDebugAndroidTest on the hosted runner.',
  );
});

test('native platforms launch with Detox synchronisation off', async () => {
  const { default: config } = await import('../.detoxrc.js');

  assert.equal(
    config.apps['android.release'].launchArgs?.detoxEnableSynchronization,
    0,
    'Android must launch with detoxEnableSynchronization 0; see the comment in .detoxrc.js.',
  );

  assert.equal(
    config.apps['ios.release'].launchArgs?.detoxEnableSynchronization,
    0,
    'iOS must launch with detoxEnableSynchronization 0; the chat never goes idle after launch.',
  );
});

// The Server can report authentication_required before iOS has laid the method buttons out.
// Detox then logs "Layers needs layout" and a 30s wait expires while the picker and Open still
// use 60s. Dropping the budget reintroduces that flake on the first (device-code) scenario.
test('the method-button tap uses the same visibility budget as the picker and Open', () => {
  const source = readFileSync(new URL('../detox/signIn.test.js', import.meta.url), 'utf8');
  assert.match(
    source,
    /tap\(`agent-auth-method-\$\{methodId\}`, 60_000\)/,
    'agent-auth-method-* must wait 60s; iOS layout after schedule is slower than the Server HTTP poll.',
  );
});
