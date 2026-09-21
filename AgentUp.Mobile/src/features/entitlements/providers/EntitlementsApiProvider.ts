import { requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { Entitlements } from '../models/Entitlements';

export async function getEntitlements(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<Entitlements | null> {
  try {
    const document = await requestServerJson<Entitlements>(server, '/api/entitlements', {}, 15000, request);
    return isEntitlements(document) ? document : null;
  } catch {
    return null;
  }
}

function isEntitlements(value: Entitlements | null | undefined): value is Entitlements {
  if (!value?.features || typeof value.features !== 'object' || Array.isArray(value.features)) return false;
  return Object.values(value.features).every(feature => feature != null && typeof feature.available === 'boolean');
}

