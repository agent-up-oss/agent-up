import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { ApplicationSpaceScreen } from '@/features/applications/components/ApplicationSpaceScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { ShellLoading } from '@/features/shell/components/ShellLoading';

export default function ApplicationSpaceRoute() {
  const { workspaceId, applicationName } = useLocalSearchParams<{ workspaceId: string; applicationName: string }>();
  const { workspaces, selectedWorkspace, selectWorkspace, ready } = useWorkspaces();
  const workspace = workspaces.find(entry => entry.id === workspaceId) ?? null;
  // useLocalSearchParams already returns decoded route parameters; decoding again would throw
  // URIError on a legitimate name that contains a percent sign, such as "100%".
  const name = applicationName ?? '';

  useEffect(() => {
    if (workspace && selectedWorkspace?.id !== workspace.id) selectWorkspace(workspace.id);
  }, [workspace, selectedWorkspace?.id, selectWorkspace]);

  if (!ready) return <ShellLoading />;
  if (!workspace || !name) return <Redirect href="/(main)/workspace" />;

  return <ApplicationSpaceScreen workspace={workspace} applicationName={name} />;
}
