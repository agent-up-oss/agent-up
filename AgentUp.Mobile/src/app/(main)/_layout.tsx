import { Redirect } from 'expo-router';
import { AppShellLayout } from '@/features/shell/components/AppShellLayout';
import { CommandCenterShell } from '@/features/interface/components/CommandCenterShell';
import { useInterfaceChoice } from '@/features/interface/controllers/InterfaceContext';
import { useServers } from '@/features/servers/controllers/ServersContext';

export default function MainLayout() {
  const { hasValidLogin } = useServers();
  const { choice } = useInterfaceChoice();
  if (!hasValidLogin) return <Redirect href="/connect" />;
  // The two shells are alternatives, not layers: the classic one keeps its routes and its
  // behaviour untouched whenever the command center is off.
  if (choice === 'command-center') return <CommandCenterShell />;
  return <AppShellLayout />;
}
