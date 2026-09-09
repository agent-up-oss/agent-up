import { Redirect } from 'expo-router';
import { useServers } from '@/features/servers/controllers/ServersContext';

export default function RootIndex() {
  const { activeServer } = useServers();
  return <Redirect href={activeServer ? '/(main)/workspace' : '/connect'} />;
}
