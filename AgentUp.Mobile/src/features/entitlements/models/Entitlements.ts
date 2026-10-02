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

export type PlanCardFeature = {
  id: string;
  available: boolean;
};

export type PlanCardLimit = {
  id: string;
  max?: number | null;
  used?: number | null;
};

export type PlanCard = {
  displayName: string;
  billing: string;
  summary: string;
  available: boolean;
  features: PlanCardFeature[];
  limits: PlanCardLimit[];
};

export type EntitlementCard = PlanCard;

export const workspaceCreateFeature = 'workspace.create';
export const workspaceCreateUnavailableMessage =
  'This Server does not allow adding workspaces from the client.';

export function isFeatureAvailable(document: Entitlements | null, feature: string): boolean {
  return document?.features?.[feature]?.available === true;
}

export function presentPlanCard(document: Entitlements | null): PlanCard {
  if (!document) {
    return {
      displayName: 'Plan unavailable',
      billing: '',
      summary: 'The Server did not return an entitlement document.',
      available: false,
      features: [],
      limits: [],
    };
  }

  const features = Object.entries(document.features ?? {}).map(([id, feature]) => ({
    id,
    available: feature.available,
  }));
  const limits = Object.entries(document.limits ?? {}).map(([id, limit]) => ({
    id,
    max: limit.max,
    used: limit.used,
  }));
  const enabled = features.filter(feature => feature.available).length;
  return {
    displayName: document.displayName,
    billing: document.billing,
    summary: `${enabled} of ${features.length} operations available`,
    available: true,
    features,
    limits,
  };
}

export function presentEntitlements(document: Entitlements | null): PlanCard {
  return presentPlanCard(document);
}
