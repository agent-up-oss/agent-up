import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { WorkspaceSettingsScreen } from '@/features/workspaces/components/WorkspaceSettingsScreen';

export default function WorkspaceSettingsRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <WorkspaceSettingsScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}
