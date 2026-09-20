import { jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { CapabilityModule } from '../models/CapabilityModule';

export async function listCapabilityModules(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<CapabilityModule[]> {
  const modules = await requestServerJson<CapabilityModule[]>(
    server,
    '/api/capabilities',
    { method: 'GET' },
    undefined,
    request,
  );
  return modules ?? [];
}

export async function enableCapabilityModule(
  server: ServerSession,
  id: string,
  version?: string,
  request: typeof fetch = fetch,
): Promise<CapabilityModule> {
  const module = await requestServerJson<CapabilityModule>(
    server,
    '/api/capabilities/enable',
    jsonBody({ id, version }),
    undefined,
    request,
  );
  if (!module) throw new Error('The server returned an empty capability module.');
  return module;
}

export async function disableCapabilityModule(
  server: ServerSession,
  id: string,
  request: typeof fetch = fetch,
): Promise<CapabilityModule> {
  const module = await requestServerJson<CapabilityModule>(
    server,
    `/api/capabilities/disable/${encodeURIComponent(id)}`,
    { method: 'POST' },
    undefined,
    request,
  );
  if (!module) throw new Error('The server returned an empty capability module.');
  return module;
}
