/**
 * Waiting helpers.
 *
 * Nothing in this suite sleeps for a fixed duration and then assumes something happened. Every
 * wait is a condition plus a deadline, and a failed wait says what it was waiting for, because a
 * fixed sleep is the single most common reason an end-to-end suite becomes flaky.
 */

const DEFAULT_TIMEOUT_MS = 60_000;
const DEFAULT_INTERVAL_MS = 100;

export async function waitFor(description, condition, { timeoutMs = DEFAULT_TIMEOUT_MS, intervalMs = DEFAULT_INTERVAL_MS } = {}) {
  const deadline = Date.now() + timeoutMs;
  let lastError;

  while (Date.now() < deadline) {
    try {
      const value = await condition();
      if (value) return value;
    } catch (cause) {
      lastError = cause;
    }

    await pause(intervalMs);
  }

  const detail = lastError ? ` Last error: ${lastError instanceof Error ? lastError.message : String(lastError)}` : '';
  throw new Error(`Timed out after ${timeoutMs}ms waiting for ${description}.${detail}`);
}

export async function waitForHttpOk(description, url, options = {}) {
  return waitFor(description, async () => {
    const response = await fetch(url);
    return response.ok ? response : false;
  }, options);
}

function pause(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}
