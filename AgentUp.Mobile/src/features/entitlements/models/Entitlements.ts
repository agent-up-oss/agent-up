export type EntitlementFeature = {
  available: boolean;
};

export type EntitlementLimit = {
  max?: number | null;
  used?: number | null;
};

export type Entitlements = {
  apiVersion: string;
  connectionId: string;
  subject: string;
  source: string;
  edition: string;
  displayName: string;
  billing: string;
  revision: string;
  expiresAt?: string | null;
  features: Record<string, EntitlementFeature>;
  limits: Record<string, EntitlementLimit>;
};

export type EntitlementCard = {
  displayName: string;
  billing: string;
  summary: string;
  available: boolean;
};

export const workspaceCreateFeature = 'workspace.create';
export const workspaceCreateUnavailableMessage =
  'This Server does not allow adding workspaces from the client.';

export function isFeatureAvailable(document: Entitlements | null, feature: string): boolean {
  return document?.features?.[feature]?.available === true;
}

export function presentEntitlements(document: Entitlements | null): EntitlementCard {
  if (!document) {
    return {
      displayName: 'Edition unavailable',
      billing: '',
      summary: 'The Server did not return an entitlement document.',
      available: false,
    };
  }

  const features = Object.values(document.features ?? {});
  const enabled = features.filter(feature => feature.available).length;
  return {
    displayName: document.displayName,
    billing: document.billing,
    summary: `${enabled} of ${features.length} operations available`,
    available: true,
  };
}
