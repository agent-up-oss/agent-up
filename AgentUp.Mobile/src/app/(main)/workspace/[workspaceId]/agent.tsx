import { Redirect, useLocalSearchParams } from 'expo-router';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { AgentChatScreen } from '@agent-up/chat';
import { GitChangesPanel } from '@/features/git/components/GitChangesPanel';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { ShellLoading } from '@/features/shell/components/ShellLoading';

export default function WorkspaceAgentRoute() {
  const { workspaceId } = useLocalSearchParams<{ workspaceId: string }>();
  const { workspaces, selectedWorkspace, selectWorkspace, ready, server } = useWorkspaces();
  const workspace = workspaces.find(entry => entry.id === workspaceId) ?? null;
  const [presentation, present] = useShellPresentation();

  useEffect(() => {
    if (workspace && selectedWorkspace?.id !== workspace.id) selectWorkspace(workspace.id);
  }, [workspace, selectedWorkspace?.id, selectWorkspace]);

  useShellConfig(presentation);

  if (!ready) return <ShellLoading />;
  if (!workspace) return <Redirect href="/(main)/workspace" />;

  // The chat module knows nothing about this app's shell or its git slice, so the route is what
  // supplies both. The harness app that the sign-in tests drive supplies neither.
  return <AgentChatScreen
    workspace={workspace}
    server={server}
    changesPanel={<GitChangesPanel workspaceId={workspace.id} />}
    onPresent={present}
  />;
}

/** Holds the chrome the chat asked for, in the shape this app's shell expects. */
function useShellPresentation() {
  const [title, setTitle] = useState('Workspace agent');
  const present = useCallback((value: { title: string }) => setTitle(value.title), []);
  const config = useMemo(() => ({ title, rightAction: null, sidebarContent: null }), [title]);
  return [config, present] as const;
}
