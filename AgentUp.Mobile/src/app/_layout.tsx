import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { StyleSheet } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { ServersProvider } from '@/features/servers/controllers/ServersContext';
import { WorkspacesProvider } from '@/features/workspaces/controllers/WorkspacesContext';

export default function RootLayout() {
  return (
    <SafeAreaProvider style={styles.safeArea}>
      <StatusBar style="light" />
      <ServersProvider>
        <WorkspacesProvider>
          <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: '#000000' } }}>
            <Stack.Screen name="index" />
            <Stack.Screen name="connect" />
            <Stack.Screen name="(main)" />
          </Stack>
        </WorkspacesProvider>
      </ServersProvider>
    </SafeAreaProvider>
  );
}

const styles = StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: '#000000' },
});
