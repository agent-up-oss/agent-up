import {
  ensureCredentialTransportAllowed,
  readProblemDetail,
  ServerRequestError,
  type ServerSession,
} from '@/features/servers/providers/ServerRequestProvider';
import type { WorkspaceStateChangedEvent } from './WorkspaceEventApplyProvider';

const MAX_BUFFER_BYTES = 1_048_576;

export function parseWorkspaceEventFrames(buffer: string): { events: WorkspaceStateChangedEvent[]; rest: string } {
  const frames = buffer.replaceAll('\r\n', '\n').split('\n\n');
  const rest = frames.pop() ?? '';
  const events = frames.flatMap(frame => {
    const data = frame.split('\n').filter(line => line.startsWith('data:')).map(line => line.slice(5).trimStart()).join('\n');
    if (!data) return [];
    const parsed = parseWorkspaceEvent(data);
    return parsed ? [parsed] : [];
  });
  return { events, rest: rest.length > MAX_BUFFER_BYTES ? '' : rest };
}

export async function streamWorkspaceEvents(
  server: ServerSession,
  onEvent: (event: WorkspaceStateChangedEvent) => void,
  signal: AbortSignal,
  request: typeof fetch = fetch,
): Promise<void> {
  if (server.accessToken) ensureCredentialTransportAllowed(server.url);

  const response = await request(`${server.url}/api/workspaces/events`, {
    headers: {
      Accept: 'text/event-stream',
      ...(server.accessToken ? { Authorization: `Bearer ${server.accessToken}` } : {}),
    },
    signal,
  });
  if (!response.ok) throw new ServerRequestError(await readProblemDetail(response), response.status);
  if (!response.body) throw new Error('Streaming responses are not supported by this device.');

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffered = '';
  while (true) {
    const result = await reader.read();
    if (result.done) break;
    buffered += decoder.decode(result.value, { stream: true });
    const parsed = parseWorkspaceEventFrames(buffered);
    buffered = parsed.rest;
    parsed.events.forEach(onEvent);
  }
  buffered += decoder.decode();
  parseWorkspaceEventFrames(buffered + '\n\n').events.forEach(onEvent);
}

function parseWorkspaceEvent(data: string): WorkspaceStateChangedEvent | null {
  try {
    const parsed = JSON.parse(data) as Partial<WorkspaceStateChangedEvent>;
    if (typeof parsed.workspaceId !== 'string' || typeof parsed.state !== 'string') return null;
    const applications = Array.isArray(parsed.applications)
      ? parsed.applications.flatMap(application => {
        if (!application || typeof application.name !== 'string' || typeof application.state !== 'string') return [];
        return [{ name: application.name, state: application.state }];
      })
      : [];
    return {
      workspaceId: parsed.workspaceId,
      state: parsed.state,
      healthState: typeof parsed.healthState === 'string' ? parsed.healthState : undefined,
      applications,
    };
  } catch {
    return null;
  }
}
