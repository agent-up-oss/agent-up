import assert from 'node:assert/strict';
import test from 'node:test';
import {
  component,
  readProcessRequest,
  scrubSensitiveHeaders,
  sentryInitOptions,
  sentryTags,
  tryCreateSentryTelemetry,
} from './SentryTelemetryProvider';

const sampleDsn = 'https://public@sentry.example/1';

test('tryCreateSentryTelemetry returns null when the DSN is missing', () => {
  assert.equal(tryCreateSentryTelemetry({ dsn: null }), null);
  assert.equal(tryCreateSentryTelemetry({ dsn: '  ' }), null);
});

test('tryCreateSentryTelemetry uses packaged production contract for production builds', () => {
  const settings = tryCreateSentryTelemetry({
    dsn: ` ${sampleDsn} `,
    nodeEnv: 'production',
    appVersion: '1.2.3',
    platform: 'web',
  });

  assert.deepEqual(settings, {
    dsn: sampleDsn,
    component,
    deployment: 'packaged',
    environment: 'production',
    release: '1.2.3',
    rid: 'web',
  });
});

test('tryCreateSentryTelemetry uses development contract outside production', () => {
  const settings = tryCreateSentryTelemetry({
    dsn: sampleDsn,
    nodeEnv: 'development',
    appVersion: '1.2.3',
  });

  assert.equal(settings?.deployment, 'development');
  assert.equal(settings?.environment, 'development');
  assert.equal(settings?.rid, null);
});

test('tryCreateSentryTelemetry honors release and environment overrides', () => {
  const settings = tryCreateSentryTelemetry({
    dsn: sampleDsn,
    nodeEnv: 'development',
    sentryEnvironment: 'production',
    sentryRelease: '9.9.9',
    appVersion: '1.2.3',
  });

  assert.equal(settings?.environment, 'production');
  assert.equal(settings?.release, '9.9.9');
});

test('sentryInitOptions keeps error-only settings and tag contract', () => {
  const settings = tryCreateSentryTelemetry({
    dsn: sampleDsn,
    nodeEnv: 'production',
    appVersion: '1.2.3',
    platform: 'web',
  });
  assert.ok(settings);

  assert.deepEqual(sentryInitOptions(settings), {
    dsn: sampleDsn,
    release: '1.2.3',
    environment: 'production',
    sendDefaultPii: false,
    tracesSampleRate: 0,
    enableAutoSessionTracking: false,
    initialScope: {
      tags: sentryTags(settings),
    },
  });
  assert.deepEqual(sentryTags(settings), {
    'agentup.component': 'mobile',
    'agentup.deployment': 'packaged',
    'agentup.rid': 'web',
  });
});

test('scrubSensitiveHeaders removes authorization and cookie headers', () => {
  const event = {
    request: {
      headers: {
        Authorization: 'Bearer secret',
        Cookie: 'session=abc',
        'Set-Cookie': 'session=abc',
        'Content-Type': 'application/json',
      },
      cookies: 'session=abc',
    },
  };

  const scrubbed = scrubSensitiveHeaders(event);

  assert.equal(scrubbed, event);
  assert.deepEqual(event.request.headers, { 'Content-Type': 'application/json' });
  assert.equal(event.request.cookies, null);
});

test('readProcessRequest maps Expo public Sentry variables', () => {
  const request = readProcessRequest({
    EXPO_PUBLIC_SENTRY_DSN: sampleDsn,
    EXPO_PUBLIC_SENTRY_ENVIRONMENT: 'production',
    EXPO_PUBLIC_SENTRY_RELEASE: '9.9.9',
    NODE_ENV: 'production',
  }, '1.2.3', 'web');

  assert.deepEqual(request, {
    dsn: sampleDsn,
    nodeEnv: 'production',
    sentryEnvironment: 'production',
    sentryRelease: '9.9.9',
    appVersion: '1.2.3',
    platform: 'web',
  });
});
