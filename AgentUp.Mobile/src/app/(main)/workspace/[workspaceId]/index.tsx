import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { WorkspaceDashboardScreen } from '@/features/workspaces/components/WorkspaceDashboardScreen';
import { WorkspaceEmptyScreen } from '@/features/workspaces/components/WorkspaceEmptyScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { ShellLoading } from '@/features/shell/components/ShellLoading';

export default function WorkspaceDashboardRoute() {
  const { workspaceId } = useLocalSearchParams<{ workspaceId: string }>();
  const { workspaces, selectedWorkspace, selectWorkspace, ready } = useWorkspaces();
  const workspace = workspaces.find(entry => entry.id === workspaceId) ?? null;

  useEffect(() => {
    if (workspace && selectedWorkspace?.id !== workspace.id) selectWorkspace(workspace.id);
  }, [workspace, selectedWorkspace?.id, selectWorkspace]);

  if (!ready) return <ShellLoading />;
  if (workspaces.length === 0) return <WorkspaceEmptyScreen />;
  if (!workspace) return <Redirect href="/(main)/workspace" />;

  return <WorkspaceDashboardScreen workspace={workspace} />;
}
