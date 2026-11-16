import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { GitReviewScreen } from '@/features/git/components/GitReviewScreen';

export default function WorkspaceGitReviewRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <GitReviewScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}
