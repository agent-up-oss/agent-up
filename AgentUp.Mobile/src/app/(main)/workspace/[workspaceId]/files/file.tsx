import { useLocalSearchParams } from 'expo-router';
import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { FileEditorScreen } from '@/features/files/components/FileEditorScreen';

export default function WorkspaceFileRoute() {
  const { path } = useLocalSearchParams<{ path?: string }>();
  return (
    <WorkspaceRouteGate>
      {workspace => <FileEditorScreen workspaceId={workspace.id} path={typeof path === 'string' ? path : ''} />}
    </WorkspaceRouteGate>
  );
}
