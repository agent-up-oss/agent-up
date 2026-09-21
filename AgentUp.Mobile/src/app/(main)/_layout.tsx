import { Redirect } from 'expo-router';
import { AppShellLayout } from '@/features/shell/components/AppShellLayout';
import { useServers } from '@/features/servers/controllers/ServersContext';

export default function MainLayout() {
  const { hasValidLogin } = useServers();
  if (!hasValidLogin) return <Redirect href="/connect" />;
  return <AppShellLayout />;
}
