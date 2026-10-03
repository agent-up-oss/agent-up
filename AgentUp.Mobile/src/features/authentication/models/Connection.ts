export const connectionApiVersion = '1';
export const connectionKindSelfHosted = 'selfHosted';
export const connectionWorkspacePresentation = 'serverScoped';
export const connectionAuthModes = ['localAdministrator', 'externalBearer', 'browserSso', 'disabled'] as const;

export type ConnectionAuthMode = (typeof connectionAuthModes)[number];
export type ConnectionKind = typeof connectionKindSelfHosted;
export type ConnectionApiVersion = typeof connectionApiVersion;
export type ConnectionWorkspacePresentation = typeof connectionWorkspacePresentation;

export type ConnectionAuthentication = {
  mode: ConnectionAuthMode;
  prompt: string;
  identifierRequired: boolean;
};

export type ConnectionMetadata = {
  apiVersion: ConnectionApiVersion;
  connectionId: string;
  kind: ConnectionKind;
  displayName: string;
  authentication: ConnectionAuthentication;
  workspacePresentation: ConnectionWorkspacePresentation;
};

export type ConnectionSource = {
  id: string;
  kind: ConnectionKind;
  baseUrl: string;
  displayName: string;
  authMode: ConnectionAuthMode;
  apiVersion: ConnectionApiVersion;
  workspacePresentation: ConnectionWorkspacePresentation;
  prompt: string;
  identifierRequired: boolean;
  isLegacy: boolean;
};

export type ConnectionSignInSurface = 'none' | 'password' | 'browserSso' | 'externalBearer';

export function isConnectionAuthMode(value: string): value is ConnectionAuthMode {
  return (connectionAuthModes as readonly string[]).includes(value);
}

export function signInSurface(authMode: ConnectionAuthMode): ConnectionSignInSurface {
  if (authMode === 'disabled') return 'none';
  if (authMode === 'localAdministrator') return 'password';
  if (authMode === 'browserSso') return 'browserSso';
  return 'externalBearer';
}

export function unknownConnectionFieldError(field: string, value: string): Error {
  return new Error(`This Server reported an unknown ${field} '${value}'.`);
}
