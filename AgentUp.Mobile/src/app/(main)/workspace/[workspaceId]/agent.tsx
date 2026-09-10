import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { AgentChatScreen } from '@/features/agents/components/AgentChatScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { ShellLoading } from '@/features/shell/components/ShellLoading';

export default function WorkspaceAgentRoute() {
  const { workspaceId } = useLocalSearchParams<{ workspaceId: string }>();
  const { workspaces, selectedWorkspace, selectWorkspace, ready } = useWorkspaces();
  const workspace = workspaces.find(entry => entry.id === workspaceId) ?? null;

  useEffect(() => {
    if (workspace && selectedWorkspace?.id !== workspace.id) selectWorkspace(workspace.id);
  }, [workspace, selectedWorkspace?.id, selectWorkspace]);

  if (!ready) return <ShellLoading />;
  if (!workspace) return <Redirect href="/(main)/workspace" />;

  return <AgentChatScreen workspace={workspace} />;
}
