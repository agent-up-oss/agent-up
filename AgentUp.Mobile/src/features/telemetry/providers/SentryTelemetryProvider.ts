export const sentryDsnVariable = 'EXPO_PUBLIC_SENTRY_DSN';
export const sentryReleaseVariable = 'EXPO_PUBLIC_SENTRY_RELEASE';
export const sentryEnvironmentVariable = 'EXPO_PUBLIC_SENTRY_ENVIRONMENT';
export const tagComponent = 'agentup.component';
export const tagDeployment = 'agentup.deployment';
export const tagRid = 'agentup.rid';
export const component = 'mobile';
export const deploymentDevelopment = 'development';
export const deploymentPackaged = 'packaged';
export const environmentDevelopment = 'development';
export const environmentProduction = 'production';

export type SentryTelemetryRequest = {
  dsn?: string | null;
  nodeEnv?: string | null;
  sentryEnvironment?: string | null;
  sentryRelease?: string | null;
  appVersion?: string | null;
  platform?: string | null;
};

export type SentryTelemetrySettings = {
  dsn: string;
  component: typeof component;
  deployment: typeof deploymentDevelopment | typeof deploymentPackaged;
  environment: string;
  release: string;
  rid: string | null;
};

export type SentryTelemetryEvent = {
  request?: {
    headers?: Record<string, string>;
    cookies?: string | null;
  };
};

export function readProcessRequest(
  env: NodeJS.ProcessEnv = process.env,
  appVersion?: string | null,
  platform?: string | null,
): SentryTelemetryRequest {
  return {
    dsn: env[sentryDsnVariable],
    nodeEnv: env.NODE_ENV,
    sentryEnvironment: env[sentryEnvironmentVariable],
    sentryRelease: env[sentryReleaseVariable],
    appVersion,
    platform,
  };
}

export function tryCreateSentryTelemetry(
  request: SentryTelemetryRequest,
): SentryTelemetrySettings | null {
  const dsn = request.dsn?.trim();
  if (!dsn) {
    return null;
  }

  const deployment = resolveDeployment(request);
  return {
    dsn,
    component,
    deployment,
    environment: resolveEnvironment(request, deployment),
    release: resolveRelease(request),
    rid: resolveRid(request),
  };
}

export function sentryInitOptions(settings: SentryTelemetrySettings) {
  return {
    dsn: settings.dsn,
    release: settings.release,
    environment: settings.environment,
    sendDefaultPii: false,
    tracesSampleRate: 0,
    enableAutoSessionTracking: false,
    initialScope: {
      tags: sentryTags(settings),
    },
  };
}

export function sentryTags(settings: SentryTelemetrySettings): Record<string, string> {
  const tags: Record<string, string> = {
    [tagComponent]: settings.component,
    [tagDeployment]: settings.deployment,
  };
  if (settings.rid) {
    tags[tagRid] = settings.rid;
  }
  return tags;
}

export function scrubSensitiveHeaders<T extends SentryTelemetryEvent>(event: T): T {
  const headers = event.request?.headers;
  if (headers) {
    delete headers.Authorization;
    delete headers.authorization;
    delete headers.Cookie;
    delete headers.cookie;
    delete headers['Set-Cookie'];
    delete headers['set-cookie'];
  }
  if (event.request) {
    event.request.cookies = null;
  }
  return event;
}

function resolveDeployment(request: SentryTelemetryRequest) {
  return request.nodeEnv === 'production' ? deploymentPackaged : deploymentDevelopment;
}

function resolveEnvironment(
  request: SentryTelemetryRequest,
  deployment: SentryTelemetrySettings['deployment'],
) {
  const configured = request.sentryEnvironment?.trim();
  if (configured) {
    return configured;
  }
  return deployment === deploymentDevelopment ? environmentDevelopment : environmentProduction;
}

function resolveRelease(request: SentryTelemetryRequest) {
  const configured = request.sentryRelease?.trim();
  if (configured) {
    return configured;
  }
  const appVersion = request.appVersion?.trim();
  if (appVersion) {
    return appVersion;
  }
  return '0.0.0';
}

function resolveRid(request: SentryTelemetryRequest) {
  const platform = request.platform?.trim();
  return platform ? platform : null;
}
