import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import { jsonBody, requestServerJson } from '@/features/servers/providers/ServerRequestProvider';
import type { AgentEvent, AgentKind, AgentSession } from '../models/AgentSession';

const root = (workspaceId: string) => `/api/workspaces/${encodeURIComponent(workspaceId)}/agent`;

export async function getAgent(server: ServerSession, workspaceId: string) {
  return requestServerJson<AgentSession>(server, root(workspaceId));
}
export async function scheduleAgent(server: ServerSession, workspaceId: string, agent: AgentKind) {
  return requestServerJson<AgentSession>(server, root(workspaceId), jsonBody({ agent }));
}
export async function sendAgentMessage(server: ServerSession, workspaceId: string, message: string) {
  return requestServerJson<null>(server, `${root(workspaceId)}/messages`, jsonBody({ message }), 0x7fffffff);
}
export async function decideAgentPermission(server: ServerSession, workspaceId: string, requestId: string, optionId: string) {
  return requestServerJson<null>(server, `${root(workspaceId)}/permissions`, jsonBody({ requestId, optionId }));
}
export async function cancelAgent(server: ServerSession, workspaceId: string) {
  return requestServerJson<null>(server, `${root(workspaceId)}/cancel`, jsonBody({}));
}
export async function stopAgent(server: ServerSession, workspaceId: string) {
  return requestServerJson<null>(server, root(workspaceId), { method: 'DELETE' });
}

export function parseSseFrames(buffer: string): { events: AgentEvent[]; rest: string } {
  const frames = buffer.replaceAll('\r\n', '\n').split('\n\n');
  const rest = frames.pop() ?? '';
  const events = frames.flatMap(frame => {
    const data = frame.split('\n').filter(line => line.startsWith('data:')).map(line => line.slice(5).trimStart()).join('\n');
    if (!data) return [];
    try { return [JSON.parse(data) as AgentEvent]; } catch { return []; }
  });
  return { events, rest };
}

export async function streamAgentEvents(
  server: ServerSession, workspaceId: string, after: number, onEvent: (event: AgentEvent) => void, signal: AbortSignal,
  request: typeof fetch = fetch,
) {
  const response = await request(`${server.url}${root(workspaceId)}/events?after=${after}`, {
    headers: { Accept: 'text/event-stream', ...(server.accessToken ? { Authorization: `Bearer ${server.accessToken}` } : {}) }, signal,
  });
  if (!response.ok) throw new Error(`Agent event stream returned ${response.status}.`);
  if (!response.body) throw new Error('Streaming responses are not supported by this device.');
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffered = '';
  while (true) {
    const result = await reader.read();
    if (result.done) break;
    buffered += decoder.decode(result.value, { stream: true });
    const parsed = parseSseFrames(buffered); buffered = parsed.rest;
    parsed.events.forEach(onEvent);
  }
}
