import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { ApplicationSpaceScreen } from '@/features/applications/components/ApplicationSpaceScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';

export default function ApplicationSpaceRoute() {
  const { workspaceId, applicationName } = useLocalSearchParams<{ workspaceId: string; applicationName: string }>();
  const { workspaces, selectedWorkspace, selectWorkspace } = useWorkspaces();
  const workspace = workspaces.find(entry => entry.id === workspaceId) ?? null;
  const decodedName = applicationName ? decodeURIComponent(applicationName) : '';

  useEffect(() => {
    if (workspace && selectedWorkspace?.id !== workspace.id) selectWorkspace(workspace.id);
  }, [workspace, selectedWorkspace?.id, selectWorkspace]);

  if (!workspace || !decodedName) return <Redirect href="/(main)/workspace" />;

  return <ApplicationSpaceScreen workspace={workspace} applicationName={decodedName} />;
}
