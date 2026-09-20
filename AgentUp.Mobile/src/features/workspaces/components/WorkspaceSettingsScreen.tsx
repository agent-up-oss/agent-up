import { useCallback, useMemo, useRef } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { CapabilityModulesSection } from '@/features/capabilities/components/CapabilitiesScreen';
import { WorkspaceTabBar } from '@/features/shell/components/WorkspaceTabBar';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { agentUpTheme } from '@agent-up/design-system/native';

export function WorkspaceSettingsScreen({ workspaceId }: { workspaceId: string }) {
  const reload = useRef<() => void>(() => undefined);
  const bindReload = useCallback((next: () => void) => { reload.current = next; }, []);

  const shellConfig = useMemo(() => ({
    title: 'Settings',
    rightAction: {
      label: 'Reload',
      accessibilityLabel: 'Reload settings',
      onPress: () => { reload.current(); },
    },
    sidebarContent: null,
  }), []);
  useShellConfig(shellConfig);

  return (
    <View style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <CapabilityModulesSection onReload={bindReload} />
      </ScrollView>
      <WorkspaceTabBar workspaceId={workspaceId} active="settings" />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: {
    padding: agentUpTheme.spacing[4],
    paddingBottom: agentUpTheme.spacing[8],
    gap: 16,
    maxWidth: 672,
    width: '100%',
    alignSelf: 'center',
  },
});
