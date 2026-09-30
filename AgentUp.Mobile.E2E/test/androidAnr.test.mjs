import assert from 'node:assert/strict';
import test from 'node:test';

import { adbDismissAnrArgs } from '../harness/androidAnr.mjs';

test('a usable focus produces no dismiss commands', () => {
  assert.deepEqual(
    adbDismissAnrArgs(
      '  mCurrentFocus=Window{abc u0 com.google.android.apps.nexuslauncher/com.google.android.apps.nexuslauncher.NexusLauncherActivity}',
    ),
    [],
  );
});

test('a setup ANR is force-stopped and the home key is sent', () => {
  const args = adbDismissAnrArgs(
    '  mCurrentFocus=Window{25aa7a4 u0 Application Not Responding: com.google.android.googlesdksetup}',
  );
  assert.deepEqual(args[0], ['shell', 'am', 'force-stop', 'com.google.android.googlesdksetup']);
  assert.deepEqual(args.at(-1), ['shell', 'input', 'keyevent', 'KEYCODE_HOME']);
});

test('a launcher ANR is force-stopped without sending HOME', () => {
  const args = adbDismissAnrArgs(
    '  mCurrentFocus=Window{99a336e u0 Application Not Responding: com.google.android.apps.nexuslauncher}',
  );
  assert.deepEqual(args[0], ['shell', 'am', 'force-stop', 'com.google.android.apps.nexuslauncher']);
  assert.equal(args.some(command => command.includes('KEYCODE_HOME')), false);
});
