import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { AgentsOverviewScreen } from '@/features/agents/components/AgentsOverviewScreen';

export default function WorkspaceAgentsRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <AgentsOverviewScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}
