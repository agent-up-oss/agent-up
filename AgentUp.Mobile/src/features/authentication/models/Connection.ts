export type ConnectionAuthentication = {
  mode: string;
  prompt: string;
  identifierRequired: boolean;
};

export type ConnectionMetadata = {
  apiVersion: string;
  connectionId: string;
  kind: string;
  displayName: string;
  authentication: ConnectionAuthentication;
  workspacePresentation: string;
};
