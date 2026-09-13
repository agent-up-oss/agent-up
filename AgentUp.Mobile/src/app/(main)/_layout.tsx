import { Redirect } from 'expo-router';
import { AppShellLayout } from '@/features/shell/components/AppShellLayout';
import { useServers } from '@/features/servers/controllers/ServersContext';

export default function MainLayout() {
  const { activeServer, requiresSignIn } = useServers();
  if (!activeServer || requiresSignIn) return <Redirect href="/connect" />;
  return <AppShellLayout />;
}
