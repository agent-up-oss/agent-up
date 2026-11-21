/**
 * An adb port reverse that is already gone counts as removed.
 *
 * A hosted emulator can drop its adb transport mid-run. Every reverse goes with it, and so does
 * the `am instrument` stream Detox drives the app through. Detox then cleans up after the dead
 * instrumentation by removing the reverse of its own WebSocket port - from inside a stream
 * `close` handler. That removal fails with "listener 'tcp:N' not found", nothing awaits the
 * handler, and the rejection is pinned on every case that runs after it: one dropped transport
 * turned three sign-ins red that never reached the app.
 *
 * Removing a mapping that no longer exists is the outcome removal asks for, so only that one
 * answer is absorbed. Any other adb failure still throws.
 */
const MISSING_LISTENER = /listener 'tcp:\d+' not found/;
const PATCHED = Symbol.for('agent-up.e2e.tolerateMissingReverse');

function isMissingReverse(error) {
  const text = `${error?.message ?? ''}\n${error?.stderr ?? ''}`;
  return MISSING_LISTENER.test(text);
}

function tolerateMissingReverse(adbPrototype) {
  if (adbPrototype[PATCHED]) return;
  const removeReverse = adbPrototype.reverseRemove;
  adbPrototype.reverseRemove = async function reverseRemove(deviceId, port) {
    try {
      return await removeReverse.call(this, deviceId, port);
    } catch (error) {
      if (isMissingReverse(error)) return undefined;
      throw error;
    }
  };
  adbPrototype[PATCHED] = true;
}

module.exports = { isMissingReverse, tolerateMissingReverse };
