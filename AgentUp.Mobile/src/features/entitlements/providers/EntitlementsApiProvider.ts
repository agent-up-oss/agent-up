import { requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { Entitlements } from '../models/Entitlements';

export async function getEntitlements(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<Entitlements | null> {
  try {
    return await requestServerJson<Entitlements>(server, '/api/entitlements', {}, 15000, request);
  } catch {
    return null;
  }
}

