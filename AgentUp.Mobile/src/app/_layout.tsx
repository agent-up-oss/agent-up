import { Stack } from 'expo-router';
import Constants from 'expo-constants';
import { StatusBar } from 'expo-status-bar';
import { Platform, StyleSheet } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { ServersProvider } from '@/features/servers/controllers/ServersContext';
import { WorkspacesProvider } from '@/features/workspaces/controllers/WorkspacesContext';
import { initializeSentry } from '@/features/telemetry/providers/SentryTelemetryInit';
import { agentUpTheme } from '@agent-up/design-system/native';

initializeSentry({
  appVersion: Constants.expoConfig?.version,
  platform: Platform.OS,
});

export default function RootLayout() {
  return (
    <SafeAreaProvider style={styles.safeArea}>
      <StatusBar style="light" />
      <ServersProvider>
        <WorkspacesProvider>
          <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: agentUpTheme.colors.canvas } }}>
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
  safeArea: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
});
