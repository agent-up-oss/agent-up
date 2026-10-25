import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { AgentPermissionPolicyScreen } from '@/features/agents/components/AgentPermissionPolicyScreen';

export default function WorkspaceAgentPolicyRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <AgentPermissionPolicyScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}
