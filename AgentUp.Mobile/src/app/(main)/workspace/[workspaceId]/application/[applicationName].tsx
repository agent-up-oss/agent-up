import { Redirect, useLocalSearchParams } from 'expo-router';
import { ApplicationSpaceScreen } from '@/features/applications/components/ApplicationSpaceScreen';
import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { useInnerShell } from '@/features/shell/hooks/useInnerShell';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';

export default function ApplicationSpaceRoute() {
  const { applicationName } = useLocalSearchParams<{ applicationName: string }>();
  const name = applicationName ?? '';

  return (
    <WorkspaceRouteGate>
      {workspace => {
        if (!name) return <Redirect href={`/(main)/workspace/${workspace.id}`} />;
        return <ApplicationInner workspace={workspace} applicationName={name} />;
      }}
    </WorkspaceRouteGate>
  );
}

function ApplicationInner({
  workspace,
  applicationName,
}: {
  workspace: Parameters<typeof ApplicationSpaceScreen>[0]['workspace'];
  applicationName: string;
}) {
  useShellConfig(useInnerShell(applicationName, `/(main)/workspace/${workspace.id}`));
  return <ApplicationSpaceScreen workspace={workspace} applicationName={applicationName} />;
}
