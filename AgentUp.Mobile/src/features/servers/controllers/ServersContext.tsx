import { createContext, useCallback, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';
import type { ConfiguredServer } from '../models/ConfiguredServer';
import {
  browserServerStorage,
  clearActiveCredential,
  loadServerSelection,
  removeServer,
  saveServerSelection,
  selectServer as selectSavedServer,
  upsertServer,
} from '../providers/ServerStorageProvider';

type ServersController = {
  servers: ConfiguredServer[];
  activeServer: ConfiguredServer | null;
  requiresSignIn: boolean;
  selectServer(id: string): void;
  saveServer(url: string, accessToken?: string): void;
  expireActiveCredential(): void;
  removeServer(id: string): void;
};

const Context = createContext<ServersController | null>(null);

export function ServersProvider({ children }: PropsWithChildren) {
  const [selection, setSelection] = useState(() => loadServerSelection(null));
  const [loaded, setLoaded] = useState(false);
  const [requiresSignIn, setRequiresSignIn] = useState(false);

  useEffect(() => {
    setSelection(loadServerSelection(browserServerStorage()));
    setLoaded(true);
  }, []);
  useEffect(() => {
    if (loaded) saveServerSelection(browserServerStorage(), selection);
  }, [loaded, selection]);

  const expireActiveCredential = useCallback(() => {
    setRequiresSignIn(true);
    setSelection(current => clearActiveCredential(current));
  }, []);

  const controller = useMemo<ServersController>(() => ({
    servers: selection.servers,
    activeServer: selection.servers.find(server => server.id === selection.activeServerId) ?? null,
    requiresSignIn,
    selectServer: id => {
      setRequiresSignIn(false);
      setSelection(current => selectSavedServer(current, id));
    },
    saveServer: (url, accessToken) => {
      setRequiresSignIn(false);
      setSelection(current => upsertServer(current, url, accessToken));
    },
    expireActiveCredential,
    removeServer: id => setSelection(current => removeServer(current, id)),
  }), [selection, requiresSignIn, expireActiveCredential]);

  return <Context.Provider value={controller}>{children}</Context.Provider>;
}

export function useServers(): ServersController {
  const value = useContext(Context);
  if (!value) throw new Error('useServers must be used inside ServersProvider.');
  return value;
}
