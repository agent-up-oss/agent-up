import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react';
import { useServers } from '@/features/servers/controllers/ServersContext';
import type { CloneSourceRequest, Workspace } from '../models/Workspace';
import { cloneSourceRepository, listWorkspaces, startWorkspace, stopWorkspace } from '../providers/WorkspacesApiProvider';
import { createWorkspaceRefresh } from '../providers/WorkspaceRefreshProvider';

type WorkspacesController = {
  serverUrl: string | null;
  workspaces: Workspace[];
  selectedWorkspace: Workspace | null;
  loading: boolean;
  error: string | null;
  selectWorkspace(id: string): void;
  refresh(): Promise<void>;
  clone(request: CloneSourceRequest): Promise<Workspace>;
  start(id: string): Promise<void>;
  stop(id: string): Promise<void>;
};

const Context = createContext<WorkspacesController | null>(null);

export function WorkspacesProvider({ children }: PropsWithChildren) {
  const { activeServer } = useServers();
  const serverUrl = activeServer?.url ?? null;
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // useState setters are stable, so the sink is created once and the refresh keeps its own
  // generation counter across renders.
  const refreshRef = useRef<((serverUrl: string | null) => Promise<void>) | null>(null);
  refreshRef.current ??= createWorkspaceRefresh({
    onLoading: setLoading,
    onWorkspaces: loaded => {
      setWorkspaces(loaded);
      setError(null);
      setSelectedId(current => (current && loaded.some(w => w.id === current) ? current : loaded[0]?.id ?? null));
    },
    onError: message => {
      setWorkspaces([]);
      setError(message);
    },
    onDisconnected: () => {
      setWorkspaces([]);
      setSelectedId(null);
      setError(null);
    },
  }, listWorkspaces);

  const refresh = useCallback(() => refreshRef.current!(serverUrl), [serverUrl]);

  useEffect(() => { void refresh(); }, [refresh]);

  const controller = useMemo<WorkspacesController>(() => ({
    serverUrl,
    workspaces,
    selectedWorkspace: workspaces.find(workspace => workspace.id === selectedId) ?? null,
    loading,
    error,
    selectWorkspace: id => setSelectedId(id),
    refresh,
    clone: async request => {
      if (!serverUrl) throw new Error('Connect this client to an Agent-Up Server first.');
      const workspace = await cloneSourceRepository(serverUrl, request);
      await refresh();
      setSelectedId(workspace.id);
      return workspace;
    },
    start: async id => {
      if (!serverUrl) return;
      await startWorkspace(serverUrl, id);
      await refresh();
    },
    stop: async id => {
      if (!serverUrl) return;
      await stopWorkspace(serverUrl, id);
      await refresh();
    },
  }), [serverUrl, workspaces, selectedId, loading, error, refresh]);

  return <Context.Provider value={controller}>{children}</Context.Provider>;
}

export function useWorkspaces(): WorkspacesController {
  const value = useContext(Context);
  if (!value) throw new Error('useWorkspaces must be used inside WorkspacesProvider.');
  return value;
}
