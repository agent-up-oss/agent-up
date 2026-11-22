import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { FilesScreen } from '@/features/files/components/FilesScreen';

export default function WorkspaceFilesRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <FilesScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}
