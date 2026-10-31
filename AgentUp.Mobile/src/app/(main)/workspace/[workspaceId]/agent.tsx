import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { WorkspaceAgentChatScreen } from '@/features/agents/components/WorkspaceAgentChatScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';

export default function WorkspaceAgentRoute() {
  const { server } = useWorkspaces();
  return (
    <WorkspaceRouteGate>
      {workspace => (
        <WorkspaceAgentChatScreen
          workspaceId={workspace.id}
          displayName={workspace.displayName}
          server={server}
        />
      )}
    </WorkspaceRouteGate>
  );
}
