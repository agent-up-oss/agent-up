import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect, type ReactNode } from 'react';
import { WorkspaceEmptyScreen } from '@/features/workspaces/components/WorkspaceEmptyScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { ShellLoading } from '@/features/shell/components/ShellLoading';

export function WorkspaceRouteGate({ children }: { children: (workspace: Workspace) => ReactNode }) {
  const { workspaceId } = useLocalSearchParams<{ workspaceId: string }>();
  const { workspaces, selectedWorkspace, selectWorkspace, ready } = useWorkspaces();
  const workspace = workspaces.find(entry => entry.id === workspaceId) ?? null;

  useEffect(() => {
    if (workspace && selectedWorkspace?.id !== workspace.id) selectWorkspace(workspace.id);
  }, [workspace, selectedWorkspace?.id, selectWorkspace]);

  if (!ready) return <ShellLoading />;
  if (workspaces.length === 0) return <WorkspaceEmptyScreen />;
  if (!workspace) return <Redirect href="/(main)/workspace" />;
  return <>{children(workspace)}</>;
}
