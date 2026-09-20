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

if (pathToFileURL(resolve(process.argv[1] ?? '')).href === import.meta.url) {
  process.exit(emulatorFocusIsUsable(process.argv[2] ?? '') ? 0 : 1);
}
