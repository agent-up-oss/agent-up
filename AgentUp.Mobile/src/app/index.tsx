import { Redirect } from 'expo-router';
import { useServers } from '@/features/servers/controllers/ServersContext';

export default function RootIndex() {
  const { activeServer, requiresSignIn } = useServers();
  return <Redirect href={activeServer && !requiresSignIn ? '/(main)/workspace' : '/connect'} />;
}
