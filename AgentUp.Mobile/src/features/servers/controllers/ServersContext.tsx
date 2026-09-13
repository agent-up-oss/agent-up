import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';
import type { ConfiguredServer } from '../models/ConfiguredServer';
import {
  browserServerStorage,
  loadServerSelection,
  removeServer,
  saveServerSelection,
  selectServer as selectSavedServer,
  upsertServer,
} from '../providers/ServerStorageProvider';

type ServersController = {
  servers: ConfiguredServer[];
  activeServer: ConfiguredServer | null;
  selectServer(id: string): void;
  saveServer(url: string, accessToken?: string): void;
  removeServer(id: string): void;
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
    selectServer: id => setSelection(current => selectSavedServer(current, id)),
    saveServer: (url, accessToken) => setSelection(current => upsertServer(current, url, accessToken)),
    removeServer: id => setSelection(current => removeServer(current, id)),
  }), [selection]);

  return <Context.Provider value={controller}>{children}</Context.Provider>;
}

export function useServers(): ServersController {
  const value = useContext(Context);
  if (!value) throw new Error('useServers must be used inside ServersProvider.');
  return value;
}
