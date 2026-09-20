import { isUnauthorized, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { WorkspaceStateChangedEvent } from './WorkspaceEventApplyProvider';

export const WORKSPACE_EVENT_MISS_DELAY_MS = 150;
export const WORKSPACE_EVENT_RECONNECT_MIN_MS = 1000;
export const WORKSPACE_EVENT_RECONNECT_MAX_MS = 30000;

export type WorkspaceEventStream = (
  server: ServerSession,
  onEvent: (event: WorkspaceStateChangedEvent) => void,
  signal: AbortSignal,
) => Promise<void>;

export type WorkspaceEventSubscriptionSink = {
  onEvent(event: WorkspaceStateChangedEvent): boolean;
  onMiss(workspaceId: string): void;
  onUnauthorized(): void;
};

export type WorkspaceEventSubscription = {
  start(server: ServerSession | null): void;
  stop(): void;
};

export type WorkspaceEventDelay = (ms: number, signal: AbortSignal) => Promise<void>;

export function createWorkspaceEventSubscription(
  sink: WorkspaceEventSubscriptionSink,
  stream: WorkspaceEventStream,
  delay: WorkspaceEventDelay = abortableDelay,
): WorkspaceEventSubscription {
  let generation = 0;
  let controller: AbortController | null = null;
  let missTimer: ReturnType<typeof setTimeout> | null = null;

  const stop = () => {
    generation += 1;
    controller?.abort();
    controller = null;
    if (missTimer) {
      clearTimeout(missTimer);
      missTimer = null;
    }
  };

  return {
    stop,
    start(server) {
      stop();
      if (!server) return;
      const ticket = generation;
      const next = new AbortController();
      controller = next;
      void run(server, ticket, next.signal);
    },
  };

  async function run(server: ServerSession, ticket: number, signal: AbortSignal): Promise<void> {
    let backoff = WORKSPACE_EVENT_RECONNECT_MIN_MS;
    while (!signal.aborted && ticket === generation) {
      try {
        await stream(server, event => {
          if (signal.aborted || ticket !== generation) return;
          const applied = sink.onEvent(event);
          if (applied || event.state === 'Removed') return;
          scheduleMiss(event.workspaceId);
        }, signal);
        backoff = WORKSPACE_EVENT_RECONNECT_MIN_MS;
        if (signal.aborted || ticket !== generation) return;
        await delay(backoff, signal);
      } catch (cause) {
        if (signal.aborted || ticket !== generation) return;
        if (isUnauthorized(cause)) {
          sink.onUnauthorized();
          return;
        }
        try {
          await delay(backoff, signal);
        } catch {
          return;
        }
        if (backoff < WORKSPACE_EVENT_RECONNECT_MAX_MS) backoff = Math.min(backoff * 2, WORKSPACE_EVENT_RECONNECT_MAX_MS);
      }
    }
  }

  function scheduleMiss(workspaceId: string): void {
    if (missTimer) clearTimeout(missTimer);
    missTimer = setTimeout(() => {
      missTimer = null;
      sink.onMiss(workspaceId);
    }, WORKSPACE_EVENT_MISS_DELAY_MS);
  }
}

export function abortableDelay(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(abortError());
      return;
    }
    const timer = setTimeout(resolve, ms);
    signal.addEventListener('abort', () => {
      clearTimeout(timer);
      reject(abortError());
    }, { once: true });
  });
}

function abortError(): Error {
  const error = new Error('Aborted');
  error.name = 'AbortError';
  return error;
}
