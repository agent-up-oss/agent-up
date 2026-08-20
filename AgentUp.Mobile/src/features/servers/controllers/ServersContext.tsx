import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';
import type { ConfiguredServer } from '../models/ConfiguredServer';
import { browserServerStorage, loadServerSelection, saveServerSelection } from '../providers/ServerStorageProvider';

type ServersController = {
  servers: ConfiguredServer[];
  activeServer: ConfiguredServer | null;
  selectServer(id: string): void;
  saveServer(url: string): void;
};

const Context = createContext<ServersController | null>(null);

export function ServersProvider({ children }: PropsWithChildren) {
  const [selection, setSelection] = useState(() => loadServerSelection(null));
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    setSelection(loadServerSelection(browserServerStorage()));
    setLoaded(true);
  }, []);
  useEffect(() => {
    if (loaded) saveServerSelection(browserServerStorage(), selection);
  }, [loaded, selection]);

  const controller = useMemo<ServersController>(() => ({
    servers: selection.servers,
    activeServer: selection.servers.find(server => server.id === selection.activeServerId) ?? null,
    selectServer: id => setSelection(current => ({ ...current, activeServerId: id })),
    saveServer: url => setSelection(current => {
      const existing = current.servers.find(server => server.url === url);
      if (existing) return { ...current, activeServerId: existing.id };
      const server = { id: `${Date.now()}-${Math.random().toString(36).slice(2)}`, url };
      return { servers: [...current.servers, server], activeServerId: server.id };
    }),
  }), [selection]);

  return <Context.Provider value={controller}>{children}</Context.Provider>;
}

export function useServers(): ServersController {
  const value = useContext(Context);
  if (!value) throw new Error('useServers must be used inside ServersProvider.');
  return value;
}
