import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { GitHistoryScreen } from '@/features/git/components/GitHistoryScreen';

export default function WorkspaceGitHistoryRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <GitHistoryScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}
