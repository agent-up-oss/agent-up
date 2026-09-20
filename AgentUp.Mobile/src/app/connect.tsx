import { useLocalSearchParams } from 'expo-router';
import { ServerSetupScreen } from '@/features/servers/components/ServerSetupScreen';

function first(value?: string | string[]): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

export default function ConnectRoute() {
  const { server, workspace } = useLocalSearchParams<{ server?: string | string[]; workspace?: string | string[] }>();
  return <ServerSetupScreen presetServerUrl={first(server)} presetWorkspaceId={first(workspace)} />;
}
