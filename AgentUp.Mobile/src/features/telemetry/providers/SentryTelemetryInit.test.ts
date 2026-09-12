import assert from 'node:assert/strict';
import test from 'node:test';
import { initializeSentry } from './SentryTelemetryInit';

const sampleDsn = 'https://public@sentry.example/1';

test('initializeSentry is a no-op without a DSN', () => {
  let initialized = false;
  const started = initializeSentry({
    sentry: { init: () => { initialized = true; } },
    env: { NODE_ENV: 'production' },
  });

  assert.equal(started, false);
  assert.equal(initialized, false);
});

test('initializeSentry inits the SDK when EXPO_PUBLIC_SENTRY_DSN is set', () => {
  let options: Record<string, unknown> | undefined;
  const started = initializeSentry({
    sentry: { init: (value) => { options = value; } },
    env: {
      EXPO_PUBLIC_SENTRY_DSN: sampleDsn,
      NODE_ENV: 'production',
    },
    appVersion: '1.2.3',
    platform: 'web',
  });

  assert.equal(started, true);
  assert.equal(options?.dsn, sampleDsn);
  assert.equal(options?.tracesSampleRate, 0);
  assert.equal(options?.sendDefaultPii, false);
});
