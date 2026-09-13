import type { ConfiguredServer } from '../models/ConfiguredServer';

const storageKey = 'agent-up.configured-servers.v1';

export type ServerSelection = {
  servers: ConfiguredServer[];
  activeServerId: string | null;
};

export interface KeyValueStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export function loadServerSelection(storage: KeyValueStorage | null): ServerSelection {
  if (!storage) return { servers: [], activeServerId: null };
  try {
    const parsed = JSON.parse(storage.getItem(storageKey) ?? 'null') as Partial<ServerSelection> | null;
    const servers = Array.isArray(parsed?.servers)
      ? parsed.servers.filter((server): server is ConfiguredServer =>
          typeof server?.id === 'string' && typeof server?.url === 'string'
          && (server.accessToken === undefined || typeof server.accessToken === 'string'))
      : [];
    const activeServerId = servers.some(server => server.id === parsed?.activeServerId)
      ? parsed?.activeServerId ?? null
      : servers[0]?.id ?? null;
    return { servers, activeServerId };
  } catch {
    return { servers: [], activeServerId: null };
  }
}

export function saveServerSelection(storage: KeyValueStorage | null, selection: ServerSelection): void {
  try {
    storage?.setItem(storageKey, JSON.stringify(selection));
  } catch {
    // Storage can be denied or exhausted; keep the in-memory provider usable.
  }
}

export function upsertServer(selection: ServerSelection, url: string, accessToken?: string): ServerSelection {
  const existing = selection.servers.find(server => server.url === url);
  if (existing) {
    return {
      servers: selection.servers.map(server => server.id === existing.id
        ? { ...server, accessToken: accessToken !== undefined ? accessToken : server.accessToken }
        : server),
      activeServerId: existing.id,
    };
  }

  const server: ConfiguredServer = {
    id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
    url,
    accessToken,
  };
  return { servers: [...selection.servers, server], activeServerId: server.id };
}

export function selectServer(selection: ServerSelection, id: string): ServerSelection {
  if (!selection.servers.some(server => server.id === id)) return selection;
  return { ...selection, activeServerId: id };
}

export function removeServer(selection: ServerSelection, id: string): ServerSelection {
  const servers = selection.servers.filter(server => server.id !== id);
  const activeServerId = selection.activeServerId === id
    ? servers[0]?.id ?? null
    : servers.some(server => server.id === selection.activeServerId)
      ? selection.activeServerId
      : servers[0]?.id ?? null;
  return { servers, activeServerId };
}

export function browserServerStorage(): KeyValueStorage | null {
  if (typeof window === 'undefined') return null;
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}
