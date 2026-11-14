import {
  jsonBody,
  requestServerJson,
  type ServerSession,
} from '@/features/servers/providers/ServerRequestProvider';
import type {
  DecisionCategoryId,
  DecisionRepository,
  DecisionVerdict,
  ProductDecision,
} from '../models/ProductDecision';

/** Every repository an agent has decided something in, with how much is waiting.  */
export async function readRepositories(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<DecisionRepository[]> {
  return (await requestServerJson<DecisionRepository[]>(
    server,
    '/api/training/repositories',
    undefined,
    undefined,
    request,
  )) ?? [];
}

/** The decisions of one repository, narrowed to a category when one is named. */
export async function readDecisions(
  server: ServerSession,
  owner: string,
  repository: string,
  category: DecisionCategoryId | null,
  request: typeof fetch = fetch,
): Promise<ProductDecision[]> {
  const query = new URLSearchParams({ owner, repository });
  if (category) query.set('category', category);
  return (await requestServerJson<ProductDecision[]>(
    server,
    `/api/training/decisions?${query.toString()}`,
    undefined,
    undefined,
    request,
  )) ?? [];
}

/** Sends what the product owner made of one decision. */
export async function reviewDecision(
  server: ServerSession,
  decisionId: string,
  verdict: DecisionVerdict,
  note: string | null,
  request: typeof fetch = fetch,
): Promise<void> {
  await requestServerJson<unknown>(
    server,
    `/api/training/decisions/${encodeURIComponent(decisionId)}/review`,
    jsonBody({ verdict, note }),
    undefined,
    request,
  );
}

/**
 * A picture behind a decision, ready to render.
 *
 * It comes back through the same authenticated read as everything else rather than as a URL on
 * an image tag: an image tag carries no Authorization header, so a real Server would refuse it
 * and the Demo would never see the request at all.
 */
export async function readEvidenceImage(
  server: ServerSession,
  evidenceId: string,
  request: typeof fetch = fetch,
): Promise<string | null> {
  const image = await requestServerJson<{ mimeType: string; base64: string }>(
    server,
    `/api/training/evidence/${encodeURIComponent(evidenceId)}`,
    undefined,
    undefined,
    request,
  );
  return image ? `data:${image.mimeType};base64,${image.base64}` : null;
}

/** Where the whole backlog is served as training material. */
export function exportUrl(server: ServerSession, includePending: boolean): string {
  return `${server.url}/api/training/export?includePending=${includePending ? 'true' : 'false'}`;
}

/** The backlog as training material, for a reader who wants to take it away. */
export async function readExport(
  server: ServerSession,
  includePending: boolean,
  request: typeof fetch = fetch,
): Promise<string> {
  const response = await request(exportUrl(server, includePending), {
    headers: server.accessToken ? { Authorization: `Bearer ${server.accessToken}` } : undefined,
  });
  if (!response.ok) throw new Error(`The Server answered ${response.status} for the export.`);
  return await response.text();
}
