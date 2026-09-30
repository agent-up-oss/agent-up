import { anrPackageFromFocus } from '../../.github/scripts/android-emulator-focus.mjs';

/**
 * adb argument lists that dismiss a focused Application Not Responding dialog.
 *
 * KEYCODE_HOME relaunches the launcher. When the launcher itself ANRed, that puts the same dialog
 * back on top of the app under test, which is how every Android scenario then missed the picker.
 */
export function adbDismissAnrArgs(focusLine) {
  const focus = String(focusLine ?? '');
  if (!focus.includes('Application Not Responding')) return [];

  const pkg = anrPackageFromFocus(focus);
  const args = [];
  if (pkg) args.push(['shell', 'am', 'force-stop', pkg]);
  args.push(['shell', 'am', 'force-stop', 'com.google.android.googlesdksetup']);
  args.push(['shell', 'am', 'force-stop', 'com.google.android.setupwizard']);
  args.push(['shell', 'input', 'keyevent', 'KEYCODE_ESCAPE']);
  args.push(['shell', 'input', 'keyevent', 'KEYCODE_BACK']);
  if (pkg !== 'com.google.android.apps.nexuslauncher') {
    args.push(['shell', 'input', 'keyevent', 'KEYCODE_HOME']);
  }
  return args;
}
