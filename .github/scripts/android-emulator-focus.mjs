import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

/**
 * Whether dumpsys window's mCurrentFocus line means Espresso can drive the device.
 *
 * A focused window is not the same as a usable one. The Google first-run wizard
 * and an Application Not Responding dialog both hold focus, and the wake script
 * used to treat that as ready. Detox then attached to a dialog the suite cannot
 * dismiss. Those windows are not ready; a launcher or the app under test is.
 */
export function emulatorFocusIsUsable(line) {
  const focus = String(line ?? '');
  if (!focus.includes('mCurrentFocus=')) return false;
  if (focus.includes('mCurrentFocus=null')) return false;
  if (focus.includes('Application Not Responding')) return false;
  if (focus.includes('com.google.android.googlesdksetup')) return false;
  if (focus.includes('com.google.android.setupwizard')) return false;
  return true;
}

/** Package named on an Application Not Responding focus line, if any. */
export function anrPackageFromFocus(line) {
  const match = String(line ?? '').match(/Application Not Responding:\s*([^}]+)/);
  return match ? match[1].trim() : '';
}

if (pathToFileURL(resolve(process.argv[1] ?? '')).href === import.meta.url) {
  const argv = process.argv.slice(2);
  if (argv[0] === '--anr-package') {
    process.stdout.write(anrPackageFromFocus(argv[1] ?? ''));
    process.exit(0);
  }
  process.exit(emulatorFocusIsUsable(argv[0] ?? '') ? 0 : 1);
}
