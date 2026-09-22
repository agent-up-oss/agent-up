import { createContext, useCallback, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';
import { hasSavedSignIn, type ConfiguredServer } from '../models/ConfiguredServer';
import {
  browserServerStorage,
  clearActiveCredential,
  loadServerSelection,
  removeServer,
  saveServerSelection,
  selectServer as selectSavedServer,
  upsertServer,
} from '../providers/ServerStorageProvider';
import { cloudServer, listSavedServers, listServers, readRecommendedServer } from '../providers/RecommendedServerProvider';
import { fakeServers } from '@/features/fake-server/controllers/FakeServerController';
import { isFakeSelection, upsertFakeServer } from '@/features/fake-server/providers/FakeServerCatalogProvider';
import { isFakeServerId, isFakeServerUrl } from '@/features/fake-server/models/FakeServerIdentity';

type ServersController = {
  servers: ConfiguredServer[];
  savedServers: ConfiguredServer[];
  cloudServer: ConfiguredServer | null;
  activeServer: ConfiguredServer | null;
  requiresSignIn: boolean;
  hasValidLogin: boolean;
  ready: boolean;
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
  const recommended = useMemo(() => readRecommendedServer(), []);

  useEffect(() => {
    const loaded = loadServerSelection(browserServerStorage());
    if (isFakeSelection(loaded)) fakeServers.activate();
    else fakeServers.deactivate();
    setSelection(loaded);
    setLoaded(true);
  }, []);
  useEffect(() => {
    if (loaded) saveServerSelection(browserServerStorage(), selection);
  }, [loaded, selection]);

  const expireActiveCredential = useCallback(() => {
    setRequiresSignIn(true);
    setSelection(current => clearActiveCredential(current));
  }, []);

  const controller = useMemo<ServersController>(() => {
    const cloud = cloudServer(selection, recommended);
    const savedServers = listSavedServers(selection, recommended);
    const servers = listServers(selection, recommended);
    const fromStore = selection.servers.find(server => server.id === selection.activeServerId);
    const activeServer = fromStore
      ? servers.find(server => server.id === fromStore.id) ?? (fromStore.url === cloud?.url ? cloud : fromStore)
      : servers.find(hasSavedSignIn) ?? null;
    const hasValidLogin = !!activeServer && !requiresSignIn && (hasSavedSignIn(activeServer) || !!activeServer.isFake);
    return {
      servers,
      savedServers,
      cloudServer: cloud,
      activeServer,
      requiresSignIn,
      hasValidLogin,
      ready: loaded,
      selectServer: id => {
        setRequiresSignIn(false);
        if (isFakeServerId(id) || servers.some(server => server.id === id && server.isFake)) {
          fakeServers.activate();
          setSelection(upsertFakeServer);
          return;
        }
        fakeServers.deactivate();
        setSelection(current => {
          if (recommended && (id === recommended.id || id === cloud?.id)
            && !current.servers.some(server => server.url === recommended.url)) {
            return upsertServer(current, recommended.url);
          }
          return selectSavedServer(current, id);
        });
      },
      saveServer: (url, accessToken) => {
        setRequiresSignIn(false);
        if (isFakeServerUrl(url)) {
          fakeServers.activate();
          setSelection(upsertFakeServer);
          return;
        }
        fakeServers.deactivate();
        setSelection(current => upsertServer(current, url, accessToken));
      },
      expireActiveCredential,
      removeServer: id => {
        const listed = servers.find(server => server.id === id);
        if (listed?.isRecommended || listed?.isFake || isFakeServerId(id)) return;
        setSelection(current => removeServer(current, id));
      },
    };
  }, [selection, requiresSignIn, expireActiveCredential, recommended, loaded]);

  return <Context.Provider value={controller}>{children}</Context.Provider>;
}

export function useServers(): ServersController {
  const value = useContext(Context);
  if (!value) throw new Error('useServers must be used inside ServersProvider.');
  return value;
}
