import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { emulatorFocusIsUsable } from './android-emulator-focus.mjs';

test('rejects an empty dumpsys line', () => {
  assert.equal(emulatorFocusIsUsable(''), false);
});

test('rejects a null focus', () => {
  assert.equal(emulatorFocusIsUsable('  mCurrentFocus=null'), false);
});

test('rejects an Application Not Responding dialog', () => {
  assert.equal(
    emulatorFocusIsUsable(
      '  mCurrentFocus=Window{25aa7a4 u0 Application Not Responding: com.google.android.googlesdksetup}',
    ),
    false,
  );
});

test('rejects the Google SDK setup wizard', () => {
  assert.equal(
    emulatorFocusIsUsable(
      '  mCurrentFocus=Window{abc u0 com.google.android.googlesdksetup/com.google.android.gms.setup.PreSetupActivity}',
    ),
    false,
  );
});

test('rejects the setup wizard', () => {
  assert.equal(
    emulatorFocusIsUsable(
      '  mCurrentFocus=Window{abc u0 com.google.android.setupwizard/.SetupWizardActivity}',
    ),
    false,
  );
});

test('accepts the launcher', () => {
  assert.equal(
    emulatorFocusIsUsable(
      '  mCurrentFocus=Window{abc u0 com.google.android.apps.nexuslauncher/com.google.android.apps.nexuslauncher.NexusLauncherActivity}',
    ),
    true,
  );
});

test('the wake helper exits non-zero for an ANR focus line', () => {
  const helper = fileURLToPath(new URL('./android-emulator-focus.mjs', import.meta.url));
  assert.throws(
    () => execFileSync(process.execPath, [
      helper,
      '  mCurrentFocus=Window{25aa7a4 u0 Application Not Responding: com.google.android.googlesdksetup}',
    ]),
    { status: 1 },
  );
});
