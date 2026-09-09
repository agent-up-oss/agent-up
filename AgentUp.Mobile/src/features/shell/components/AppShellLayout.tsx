import { Redirect, Stack } from 'expo-router';
import { StyleSheet, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { AppShellProvider } from '../controllers/AppShellContext';
import { AppNavBar } from './AppNavBar';
import { WorkspaceSidebar } from './WorkspaceSidebar';

function AuthenticatedShell() {
  const { activeServer } = useServers();
  if (!activeServer) return <Redirect href="/connect" />;

  return (
    <AppShellProvider>
      <View style={styles.shell}>
        <AppNavBar />
        <View style={styles.content}>
          <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: '#000000' } }} />
        </View>
        <WorkspaceSidebar />
      </View>
    </AppShellProvider>
  );
}

export function AppShellLayout() {
  return <AuthenticatedShell />;
}

const styles = StyleSheet.create({
  shell: { flex: 1, backgroundColor: '#000000' },
  content: { flex: 1 },
});
