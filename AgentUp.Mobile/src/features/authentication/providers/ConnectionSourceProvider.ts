import {
  connectionApiVersion,
  connectionKindSelfHosted,
  connectionWorkspacePresentation,
  isConnectionAuthMode,
  signInSurface,
  unknownConnectionFieldError,
  type ConnectionAuthMode,
  type ConnectionMetadata,
  type ConnectionSignInSurface,
  type ConnectionSource,
} from '../models/Connection';
import { getAuthenticationStatus } from './AuthenticationProvider';

export type { ConnectionSignInSurface, ConnectionSource };

export function parseConnectionSource(baseUrl: string, value: unknown): ConnectionSource {
  const document = asRecord(value);
  const apiVersion = requiredString(document, 'apiVersion');
  if (apiVersion !== connectionApiVersion) throw unknownConnectionFieldError('apiVersion', apiVersion);

  const kind = requiredString(document, 'kind');
  if (kind !== connectionKindSelfHosted) throw unknownConnectionFieldError('kind', kind);

  const workspacePresentation = requiredString(document, 'workspacePresentation');
  if (workspacePresentation !== connectionWorkspacePresentation) {
    throw unknownConnectionFieldError('workspacePresentation', workspacePresentation);
  }

  const authentication = asRecord(document.authentication);
  const mode = requiredString(authentication, 'mode');
  if (!isConnectionAuthMode(mode)) throw unknownConnectionFieldError('authentication.mode', mode);

  const connectionId = requiredString(document, 'connectionId');
  const displayName = requiredString(document, 'displayName');
  const prompt = requiredString(authentication, 'prompt');
  return {
    id: connectionId,
    kind: connectionKindSelfHosted,
    baseUrl,
    displayName,
    authMode: mode,
    apiVersion: connectionApiVersion,
    workspacePresentation: connectionWorkspacePresentation,
    prompt,
    identifierRequired: authentication.identifierRequired === true,
    isLegacy: false,
  };
}

export function legacySelfHostedConnection(baseUrl: string, authenticationRequired: boolean): ConnectionSource {
  const authMode: ConnectionAuthMode = authenticationRequired ? 'localAdministrator' : 'disabled';
  return {
    id: 'legacy',
    kind: connectionKindSelfHosted,
    baseUrl,
    displayName: displayNameFromUrl(baseUrl),
    authMode,
    apiVersion: connectionApiVersion,
    workspacePresentation: connectionWorkspacePresentation,
    prompt: authenticationRequired
      ? 'Enter the administrator password to continue.'
      : 'Authentication is not required for this Server.',
    identifierRequired: false,
    isLegacy: true,
  };
}

export function connectionSignInSurface(source: ConnectionSource): ConnectionSignInSurface {
  return signInSurface(source.authMode);
}

export async function resolveConnectionSource(
  url: string,
  request: typeof fetch = fetch,
): Promise<ConnectionSource> {
  const connection = await readConnectionDocument(url, request);
  if (connection.status === 'missing') {
    const auth = await getAuthenticationStatus(url, request);
    return legacySelfHostedConnection(url, auth.authenticationRequired);
  }

  return parseConnectionSource(url, connection.document);
}

async function readConnectionDocument(
  url: string,
  request: typeof fetch,
): Promise<{ status: 'ok'; document: unknown } | { status: 'missing' }> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 8000);
  try {
    const response = await request(`${url}/api/connection`, {
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    });
    if (response.status === 404) return { status: 'missing' };
    if (!response.ok) throw new Error(`Server returned ${response.status}.`);
    return { status: 'ok', document: await response.json() };
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      throw new Error('The server did not respond in time.');
    }
    throw error;
  } finally {
    clearTimeout(timeout);
  }
}

function displayNameFromUrl(baseUrl: string): string {
  try {
    return new URL(baseUrl).host || 'Agent-Up Server';
  } catch {
    return 'Agent-Up Server';
  }
}

function asRecord(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error('This Server did not return a connection document.');
  }
  return value as Record<string, unknown>;
}

function requiredString(document: Record<string, unknown>, field: string): string {
  const value = document[field];
  if (typeof value !== 'string' || value.trim() === '') {
    throw new Error(`This Server did not return a connection ${field}.`);
  }
  return value;
}

export type { ConnectionMetadata };
