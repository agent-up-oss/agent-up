/**
 * What the native suite should do after launchApp, given which harness surfaces are visible.
 *
 * Waiting only for the picker, then peeking at the connect form for 500ms, is how a missed deep
 * link skipped Connect and then waited a minute for a picker the chat never mounted.
 */
export function connectStep({ pickerVisible, formVisible, promptVisible }) {
  if (pickerVisible) return 'connected';
  if (formVisible) return 'fill-form';
  if (promptVisible) return 'wait-agents';
  return 'relaunch';
}
