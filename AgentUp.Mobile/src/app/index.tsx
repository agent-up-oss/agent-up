import { Redirect } from 'expo-router';
import { useServers } from '@/features/servers/controllers/ServersContext';

export default function RootIndex() {
  const { hasValidLogin } = useServers();
  return <Redirect href={hasValidLogin ? '/(main)/workspace' : '/connect'} />;
}
