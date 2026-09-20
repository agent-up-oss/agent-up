import assert from 'node:assert/strict';
import { test } from 'node:test';
import { ServerRequestError, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { WorkspaceStateChangedEvent } from './WorkspaceEventApplyProvider';
import {
  WORKSPACE_EVENT_MISS_DELAY_MS,
  WORKSPACE_EVENT_RECONNECT_MIN_MS,
  abortableDelay,
  createWorkspaceEventSubscription,
} from './WorkspaceEventsSubscriptionProvider';

function at(url: string): ServerSession {
  return { url };
}

function event(workspaceId: string, state = 'Running'): WorkspaceStateChangedEvent {
  return { workspaceId, state, applications: [] };
}

function deferred<T = void>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(next => { resolve = next; });
  return { promise, resolve };
}

async function waitForAbort(signal: AbortSignal): Promise<void> {
  if (signal.aborted) return;
  await new Promise<void>(resolve => {
    signal.addEventListener('abort', () => resolve(), { once: true });
  });
}

test('applies events from the live stream and reconnects after a clean close', async () => {
  const applied: string[] = [];
  let connections = 0;
  const secondStarted = deferred();
  const subscription = createWorkspaceEventSubscription(
    {
      onEvent: evt => { applied.push(evt.state); return true; },
      onMiss: () => { throw new Error('known workspaces must not miss'); },
      onUnauthorized: () => { throw new Error('authorized streams must not expire'); },
    },
    async (_server, onEvent, signal) => {
      connections += 1;
      if (connections === 1) {
        onEvent(event('ws-1', 'Starting'));
        return;
      }
      secondStarted.resolve();
      onEvent(event('ws-1', 'Running'));
      await waitForAbort(signal);
    },
    async (_ms, signal) => {
      if (signal.aborted) {
        const error = new Error('Aborted');
        error.name = 'AbortError';
        throw error;
      }
    },
  );

  subscription.start(at('https://server.example'));
  await secondStarted.promise;
  subscription.stop();

  assert.equal(connections, 2);
  assert.deepEqual(applied, ['Starting', 'Running']);
});

test('debounces a refresh when the event names a workspace the client does not have', async () => {
  const missed: string[] = [];
  const closed = deferred();
  const subscription = createWorkspaceEventSubscription(
    {
      onEvent: () => false,
      onMiss: id => { missed.push(id); },
      onUnauthorized: () => { throw new Error('unauthorized must not fire'); },
    },
    async (_server, onEvent, signal) => {
      onEvent(event('ws-new', 'Starting'));
      onEvent(event('ws-new', 'Running'));
      closed.resolve();
      await waitForAbort(signal);
    },
    async (_ms, signal) => waitForAbort(signal),
  );

  subscription.start(at('https://server.example'));
  await closed.promise;
  await new Promise(resolve => setTimeout(resolve, WORKSPACE_EVENT_MISS_DELAY_MS + 20));
  subscription.stop();

  assert.deepEqual(missed, ['ws-new']);
});

test('stops reconnecting after an unauthorized stream', async () => {
  let connections = 0;
  let unauthorized = 0;
  const finished = deferred();
  const subscription = createWorkspaceEventSubscription(
    {
      onEvent: () => true,
      onMiss: () => { throw new Error('miss must not fire'); },
      onUnauthorized: () => {
        unauthorized += 1;
        finished.resolve();
      },
    },
    async () => {
      connections += 1;
      throw new ServerRequestError('The server returned 401.', 401);
    },
    async () => { throw new Error('unauthorized streams must not delay'); },
  );

  subscription.start(at('https://server.example'));
  await finished.promise;
  subscription.stop();

  assert.equal(connections, 1);
  assert.equal(unauthorized, 1);
});

test('resets reconnect backoff when a stream delivers an event then fails', async () => {
  const delays: number[] = [];
  let connections = 0;
  const thirdStarted = deferred();
  const subscription = createWorkspaceEventSubscription(
    {
      onEvent: () => true,
      onMiss: () => { throw new Error('miss must not fire'); },
      onUnauthorized: () => { throw new Error('unauthorized must not fire'); },
    },
    async (_server, onEvent, signal) => {
      connections += 1;
      if (connections <= 2) {
        onEvent(event('ws-1', 'Running'));
        throw new Error('socket closed');
      }
      thirdStarted.resolve();
      await waitForAbort(signal);
    },
    async (ms, signal) => {
      delays.push(ms);
      if (signal.aborted) {
        const error = new Error('Aborted');
        error.name = 'AbortError';
        throw error;
      }
    },
  );

  subscription.start(at('https://server.example'));
  await thirdStarted.promise;
  subscription.stop();

  assert.equal(connections, 3);
  assert.deepEqual(delays, [WORKSPACE_EVENT_RECONNECT_MIN_MS, WORKSPACE_EVENT_RECONNECT_MIN_MS]);
});

test('increases reconnect backoff when a stream fails before any event', async () => {
  const delays: number[] = [];
  let connections = 0;
  const thirdStarted = deferred();
  const subscription = createWorkspaceEventSubscription(
    {
      onEvent: () => true,
      onMiss: () => { throw new Error('miss must not fire'); },
      onUnauthorized: () => { throw new Error('unauthorized must not fire'); },
    },
    async (_server, _onEvent, signal) => {
      connections += 1;
      if (connections <= 2) throw new Error('socket closed');
      thirdStarted.resolve();
      await waitForAbort(signal);
    },
    async (ms, signal) => {
      delays.push(ms);
      if (signal.aborted) {
        const error = new Error('Aborted');
        error.name = 'AbortError';
        throw error;
      }
    },
  );

  subscription.start(at('https://server.example'));
  await thirdStarted.promise;
  subscription.stop();

  assert.deepEqual(delays, [WORKSPACE_EVENT_RECONNECT_MIN_MS, WORKSPACE_EVENT_RECONNECT_MIN_MS * 2]);
});

test('abortableDelay removes the abort listener when it resolves', async () => {
  const listeners = new Set<() => void>();
  const signal = {
    aborted: false,
    addEventListener(_type: string, listener: () => void) { listeners.add(listener); },
    removeEventListener(_type: string, listener: () => void) { listeners.delete(listener); },
  } as unknown as AbortSignal;

  await abortableDelay(0, signal);

  assert.equal(listeners.size, 0);
});

test('abortableDelay rejects when aborted before it elapses', async () => {
  const controller = new AbortController();
  const pending = abortableDelay(60_000, controller.signal);
  controller.abort();
  await assert.rejects(() => pending, { name: 'AbortError' });
});
