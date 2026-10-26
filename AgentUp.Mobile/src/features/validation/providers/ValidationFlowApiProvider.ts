import { requestServerJson, ServerRequestError, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { ValidationFlow } from '../models/ValidationFlow';

/** Lists Application-surface checks recorded for one workspace application. */
export async function listValidationFlows(
  server: ServerSession,
  workspaceId: string,
  application: string,
  request: typeof fetch = fetch,
): Promise<ValidationFlow[]> {
  const path = `/api/workspaces/${encodeURIComponent(workspaceId)}/validation-flows?application=${encodeURIComponent(application)}`;
  const flows = await requestServerJson<ValidationFlow[]>(server, path, { method: 'GET' }, undefined, request);
  return flows ?? [];
}

/** Returns one flow, or null when the Server has no check with that id. */
export async function getValidationFlow(
  server: ServerSession,
  workspaceId: string,
  flowId: string,
  request: typeof fetch = fetch,
): Promise<ValidationFlow | null> {
  const path = `/api/workspaces/${encodeURIComponent(workspaceId)}/validation-flows/${encodeURIComponent(flowId)}`;
  try {
    return await requestServerJson<ValidationFlow>(server, path, { method: 'GET' }, undefined, request);
  } catch (error) {
    if (error instanceof ServerRequestError && error.status === 404) return null;
    throw error;
  }
}
