import { fakeServerEventFrame } from '../models/FakeServerEvent';
import { isFakeServerUrl } from '../models/FakeServerIdentity';
import { FakeBackendService } from '../services/FakeBackendService';

let installed: typeof fetch | null = null;
let original: typeof fetch | null = null;
let backend: FakeBackendService | null = null;

export function createFakeServerFetch(service: FakeBackendService, realFetch: typeof fetch): typeof fetch {
  return async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = toUrl(input, init);
    if (!isFakeServerUrl(url.origin))
      return realFetch(input, init);

    const method = (init?.method ?? (input instanceof Request ? input.method : 'GET')).toUpperCase();
    const path = url.pathname;
    const query = url.search;
    const body = await readBody(input, init);
    const request = { method, path, query, body };

    if (isAgentEventRoute(path))
      return agentEventResponse(service, path, query, request, init?.signal);
    if (isWorkspaceEventRoute(path))
      return workspaceEventResponse(service, init?.signal);

    const result = service.handle(request);
    if (result.status === 204)
      return new Response(null, { status: 204 });
    return new Response(result.body ?? '', {
      status: result.status,
      headers: { 'Content-Type': result.contentType },
    });
  };
}

export function installFakeServerFetch(service: FakeBackendService): void {
  if (installed) {
    backend = service;
    return;
  }
  original = globalThis.fetch.bind(globalThis);
  backend = service;
  installed = createFakeServerFetch(service, original);
  globalThis.fetch = ((input: RequestInfo | URL, init?: RequestInit) =>
    createFakeServerFetch(backend!, original!)(input, init)) as typeof fetch;
}

export function uninstallFakeServerFetch(): void {
  if (!original) return;
  globalThis.fetch = original;
  original = null;
  installed = null;
  backend = null;
}

export function isFakeServerFetchInstalled(): boolean {
  return installed !== null;
}

function agentEventResponse(
  service: FakeBackendService,
  path: string,
  query: string,
  request: { method: string; path: string; query: string; body: string | null },
  signal?: AbortSignal | null,
): Response {
  const workspaceId = workspaceIdFrom(path);
  if (!workspaceId) {
    const result = service.handle(request);
    return new Response(result.body ?? '', { status: result.status, headers: { 'Content-Type': result.contentType } });
  }
  const after = Number(new URLSearchParams(query.startsWith('?') ? query.slice(1) : query).get('after') ?? '0') || 0;
  return sseResponse(controller => {
    service.agentEventsAfter(workspaceId, after).forEach(item => controller.enqueue(encode(fakeServerEventFrame(item))));
    return service.subscribeAgent(workspaceId, item => controller.enqueue(encode(fakeServerEventFrame(item))));
  }, signal);
}

function workspaceEventResponse(service: FakeBackendService, signal?: AbortSignal | null): Response {
  return sseResponse(controller => {
    controller.enqueue(encode(service.workspaceSnapshotSse()));
    return service.subscribeWorkspaces(snapshot => controller.enqueue(encode(snapshot)));
  }, signal);
}

function sseResponse(
  subscribe: (controller: ReadableStreamDefaultController<Uint8Array>) => () => void,
  signal?: AbortSignal | null,
): Response {
  let unsubscribe = () => {};
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      unsubscribe = subscribe(controller);
      signal?.addEventListener('abort', () => {
        unsubscribe();
        controller.close();
      }, { once: true });
    },
    cancel() {
      unsubscribe();
    },
  });
  return new Response(stream, { headers: { 'Content-Type': 'text/event-stream' } });
}

function encode(value: string): Uint8Array {
  return new TextEncoder().encode(value);
}

function toUrl(input: RequestInfo | URL, _init?: RequestInit): URL {
  if (input instanceof URL) return input;
  if (typeof input === 'string') return new URL(input);
  return new URL(input.url);
}

async function readBody(input: RequestInfo | URL, init?: RequestInit): Promise<string | null> {
  if (typeof init?.body === 'string') return init.body;
  if (input instanceof Request) return input.text();
  return null;
}

function isAgentEventRoute(path: string): boolean {
  return path.startsWith('/api/workspaces/') && path.endsWith('/agent/events');
}

function isWorkspaceEventRoute(path: string): boolean {
  return path.replace(/\/+$/, '') === '/api/workspaces/events';
}

function workspaceIdFrom(path: string): string | null {
  const match = /^\/api\/workspaces\/([^/]+)\/agent\/events$/.exec(path);
  return match ? decodeURIComponent(match[1]) : null;
}
