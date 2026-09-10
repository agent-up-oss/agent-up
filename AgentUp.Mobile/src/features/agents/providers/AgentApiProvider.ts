import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import { jsonBody, requestServerJson } from '@/features/servers/providers/ServerRequestProvider';
import type { AgentEvent, AgentKind, AgentSession } from '../models/AgentSession';

const root = (workspaceId: string) => `/api/workspaces/${encodeURIComponent(workspaceId)}/agent`;

export async function getAgent(server: ServerSession, workspaceId: string, request: typeof fetch = fetch) {
  return requestServerJson<AgentSession>(server, root(workspaceId), {}, undefined, request);
}
export async function scheduleAgent(server: ServerSession, workspaceId: string, agent: AgentKind, request: typeof fetch = fetch) {
  return requestServerJson<AgentSession>(server, root(workspaceId), jsonBody({ agent }), undefined, request);
}
export async function sendAgentMessage(server: ServerSession, workspaceId: string, message: string, request: typeof fetch = fetch) {
  return requestServerJson<null>(server, `${root(workspaceId)}/messages`, jsonBody({ message }), undefined, request);
}
export async function authenticateAgent(server: ServerSession, workspaceId: string, methodId: string, request: typeof fetch = fetch) {
  return requestServerJson<null>(server, `${root(workspaceId)}/authenticate`, jsonBody({ methodId }), undefined, request);
}
export async function decideAgentPermission(server: ServerSession, workspaceId: string, requestId: string, optionId: string, request: typeof fetch = fetch) {
  return requestServerJson<null>(server, `${root(workspaceId)}/permissions`, jsonBody({ requestId, optionId }), undefined, request);
}
export async function cancelAgent(server: ServerSession, workspaceId: string, request: typeof fetch = fetch) {
  return requestServerJson<null>(server, `${root(workspaceId)}/cancel`, jsonBody({}), undefined, request);
}
export async function stopAgent(server: ServerSession, workspaceId: string, request: typeof fetch = fetch) {
  return requestServerJson<null>(server, root(workspaceId), { method: 'DELETE' }, undefined, request);
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
  buffered += decoder.decode();
  const final = parseSseFrames(buffered + '\n\n');
  final.events.forEach(onEvent);
}
