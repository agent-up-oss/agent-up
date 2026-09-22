export type CapabilityModule = {
  id: string;
  version: string;
  displayName: string;
  publisher: string;
  kind: string;
  enabled: boolean;
  state: string;
  canRun: boolean;
  messages: string[];
};

export type CapabilityStatus = {
  capabilityId: string;
  requiredVersion?: string | null;
  canRun: boolean;
  messages: string[];
};
