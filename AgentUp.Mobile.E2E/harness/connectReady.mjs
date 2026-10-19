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

// A blank frame is not a missed launch. The deep link often paints the picker a moment after
// launchApp returns. Treating that first empty snapshot as 'relaunch' starts a second launch
// while the first one is already opening the event stream, and Detox then waits on the connect
// form of an app that never goes idle.
export function connectStepPending(step) {
  return step === 'wait-agents' || step === 'relaunch';
}
