import {
  readProcessRequest,
  scrubSensitiveHeaders,
  sentryInitOptions,
  tryCreateSentryTelemetry,
} from './SentryTelemetryProvider';

type SentryModule = {
  init: (options: Record<string, unknown>) => void;
};

export type InitializeSentryOptions = {
  sentry?: SentryModule | null;
  env?: NodeJS.ProcessEnv;
  appVersion?: string | null;
  platform?: string | null;
};

export function initializeSentry(options: InitializeSentryOptions = {}): boolean {
  const settings = tryCreateSentryTelemetry(
    readProcessRequest(options.env ?? process.env, options.appVersion, options.platform),
  );
  if (!settings) {
    return false;
  }

  const sdk = options.sentry === undefined ? loadSentrySdk() : options.sentry;
  if (!sdk) {
    return false;
  }

  sdk.init({
    ...sentryInitOptions(settings),
    beforeSend(event: { request?: { headers?: Record<string, string>; cookies?: string | null } }) {
      return scrubSensitiveHeaders(event);
    },
  });
  return true;
}

function loadSentrySdk(): SentryModule | null {
  try {
    // Lazy so unit tests can cover gating without loading native Sentry.
    return require('@sentry/react-native') as SentryModule;
  } catch {
    return null;
  }
}
