import { useCallback, useMemo, useRef } from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { AgentLoopSettingsSection } from '@/features/agents/components/AgentLoopSettingsSection';
import { CapabilityModulesSection } from '@/features/capabilities/components/CapabilitiesScreen';
import { InterfaceToggleRow } from '@/features/interface/components/InterfaceToggleRow';
import { WorkspacePage } from '@/features/shell/components/WorkspacePage';
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
    <WorkspacePage workspaceId={workspaceId} active="settings">
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <InterfaceToggleRow />
        <AgentLoopSettingsSection workspaceId={workspaceId} />
        <CapabilityModulesSection onReload={bindReload} />
      </ScrollView>
    </WorkspacePage>
  );
}

const styles = StyleSheet.create({
  content: {
    padding: agentUpTheme.spacing[4],
    paddingBottom: agentUpTheme.spacing[8],
    gap: 16,
    width: '100%',
  },
});
