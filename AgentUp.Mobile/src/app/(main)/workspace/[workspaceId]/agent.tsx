import { Redirect, useLocalSearchParams } from 'expo-router';
import { useEffect } from 'react';
import { AgentChatScreen } from '@/features/agents/components/AgentChatScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';

export default function WorkspaceAgentRoute() {
  const { workspaceId } = useLocalSearchParams<{ workspaceId: string }>();
  const { workspaces, selectedWorkspace, selectWorkspace } = useWorkspaces();
  const workspace = workspaces.find(entry => entry.id === workspaceId) ?? null;

  useEffect(() => {
    if (workspace && selectedWorkspace?.id !== workspace.id) selectWorkspace(workspace.id);
  }, [workspace, selectedWorkspace?.id, selectWorkspace]);

  if (!workspace) return <Redirect href="/(main)/workspace" />;

  return <AgentChatScreen workspace={workspace} />;
}
