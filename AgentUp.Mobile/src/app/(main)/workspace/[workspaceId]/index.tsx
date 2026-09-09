import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { WorkspaceDashboardScreen } from '@/features/workspaces/components/WorkspaceDashboardScreen';
import { WorkspaceEmptyScreen } from '@/features/workspaces/components/WorkspaceEmptyScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';

export default function WorkspaceDashboardRoute() {
  const { workspaceId } = useLocalSearchParams<{ workspaceId: string }>();
  const { workspaces, selectedWorkspace, selectWorkspace, loading } = useWorkspaces();
  const workspace = workspaces.find(entry => entry.id === workspaceId) ?? null;

  useEffect(() => {
    if (workspace && selectedWorkspace?.id !== workspace.id) selectWorkspace(workspace.id);
  }, [workspace, selectedWorkspace?.id, selectWorkspace]);

  if (!loading && workspaces.length === 0) return <WorkspaceEmptyScreen />;
  if (!workspace) return <Redirect href="/(main)/workspace" />;

  return <WorkspaceDashboardScreen workspace={workspace} />;
}
