import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { WorkspaceDashboardScreen } from '@/features/workspaces/components/WorkspaceDashboardScreen';

export default function WorkspaceDashboardRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <WorkspaceDashboardScreen workspace={workspace} />}
    </WorkspaceRouteGate>
  );
}
