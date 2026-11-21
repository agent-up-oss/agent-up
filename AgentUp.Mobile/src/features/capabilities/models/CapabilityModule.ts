/** One value a user configures for a capability. A secret's value is never returned; `hasValue` says whether one is stored. */
export type CapabilitySetting = {
  key: string;
  label: string;
  kind: 'text' | 'url' | 'secret' | string;
  required: boolean;
  placeholder?: string | null;
  value?: string | null;
  hasValue: boolean;
};

/** A capability built into the Server. `available` is whether the Server found the software it runs. */
export type CapabilityModule = {
  id: string;
  version: string;
  displayName: string;
  publisher: string;
  kind: string;
  enabled: boolean;
  available?: boolean;
  state: string;
  canRun: boolean;
  missing?: string[];
  settings?: CapabilitySetting[];
  messages: string[];
};

export type CapabilityStatus = {
  capabilityId: string;
  requiredVersion?: string | null;
  canRun: boolean;
  messages: string[];
};
