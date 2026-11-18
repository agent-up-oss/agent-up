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
  request: typeof fetch = fetch,
): Promise<CapabilityModule> {
  const module = await requestServerJson<CapabilityModule>(
    server,
    '/api/capabilities/enable',
    jsonBody({ id }),
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

/** Changes only the given settings; a key left out keeps its stored value, which is how a secret survives a save. */
export async function updateCapabilitySettings(
  server: ServerSession,
  id: string,
  values: Record<string, string>,
  request: typeof fetch = fetch,
): Promise<CapabilityModule> {
  const module = await requestServerJson<CapabilityModule>(
    server,
    `/api/capabilities/${encodeURIComponent(id)}/settings`,
    { ...jsonBody({ values }), method: 'PUT' },
    undefined,
    request,
  );
  if (!module) throw new Error('The server returned an empty capability module.');
  return module;
}
