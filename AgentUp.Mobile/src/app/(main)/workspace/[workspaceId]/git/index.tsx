import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { GitOverviewScreen } from '@/features/git/components/GitOverviewScreen';

export default function WorkspaceGitOverviewRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <GitOverviewScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}
