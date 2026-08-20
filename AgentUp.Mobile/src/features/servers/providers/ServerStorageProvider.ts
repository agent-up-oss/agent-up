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
          typeof server?.id === 'string' && typeof server?.url === 'string')
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
  storage?.setItem(storageKey, JSON.stringify(selection));
}

export function browserServerStorage(): KeyValueStorage | null {
  return typeof window === 'undefined' ? null : window.localStorage;
}
