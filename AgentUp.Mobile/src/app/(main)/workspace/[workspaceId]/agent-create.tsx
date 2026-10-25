import { AgentCreationScreen } from '@/features/agents/components/AgentCreationScreen';
import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';

export default function WorkspaceAgentCreateRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <AgentCreationScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}
