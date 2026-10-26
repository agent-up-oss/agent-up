import { Redirect, useLocalSearchParams } from 'expo-router';
import { ApplicationSpaceScreen } from '@/features/applications/components/ApplicationSpaceScreen';
import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';

export default function ApplicationSpaceRoute() {
  const { applicationName } = useLocalSearchParams<{ applicationName: string }>();
  const name = applicationName ?? '';

  return (
    <WorkspaceRouteGate>
      {workspace => {
        if (!name) return <Redirect href={`/(main)/workspace/${workspace.id}`} />;
        return <ApplicationSpaceScreen workspace={workspace} applicationName={name} />;
      }}
    </WorkspaceRouteGate>
  );
}
