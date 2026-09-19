import { AgentChatScreen } from '@agent-up/chat';
import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { useInnerShell } from '@/features/shell/hooks/useInnerShell';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useCallback, useMemo, useState } from 'react';

export default function WorkspaceAgentRoute() {
  const { server } = useWorkspaces();
  return (
    <WorkspaceRouteGate>
      {workspace => <WorkspaceAgentChat workspaceId={workspace.id} displayName={workspace.displayName} server={server} />}
    </WorkspaceRouteGate>
  );
}

function WorkspaceAgentChat({
  workspaceId,
  displayName,
  server,
}: {
  workspaceId: string;
  displayName: string;
  server: ReturnType<typeof useWorkspaces>['server'];
}) {
  const [title, setTitle] = useState('Workspace agent');
  const present = useCallback((value: { title: string }) => setTitle(value.title), []);
  const inner = useInnerShell(title, `/(main)/workspace/${workspaceId}/agents`);
  const config = useMemo(() => ({ ...inner, title }), [inner, title]);
  useShellConfig(config);

  return <AgentChatScreen
    workspace={{ id: workspaceId, displayName }}
    server={server}
    onPresent={present}
  />;
}
