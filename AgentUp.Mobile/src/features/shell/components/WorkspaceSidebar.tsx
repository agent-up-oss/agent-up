import { useRouter } from 'expo-router';
import { type ReactNode } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { useAppShell } from '../controllers/AppShellContext';
import { agentUpTheme } from '@agent-up/design-system/native';

function workspaceStateColor(state: string): string {
  if (state === 'Running') return agentUpTheme.colors.accent;
  if (state === 'Starting' || state === 'Stopping') return agentUpTheme.colors.statusWarning;
  if (state === 'Failed') return agentUpTheme.colors.statusDanger;
  return agentUpTheme.colors.textFaint;
}

function DefaultSidebarContent({ onNavigate }: { onNavigate: () => void }) {
  const router = useRouter();
  const { activeServer } = useServers();
  const { workspaces, selectedWorkspace, selectWorkspace } = useWorkspaces();

  return (
    <View style={styles.defaultContent}>
      <Text style={styles.sectionLabel}>Workspaces</Text>
      <ScrollView contentContainerStyle={styles.workspaceList}>
        {workspaces.map(workspace => {
          const isSelected = selectedWorkspace?.id === workspace.id;
          return (
            <Pressable
              key={workspace.id}
              accessibilityRole="button"
              accessibilityState={{ selected: isSelected }}
              accessibilityLabel={`Open workspace ${workspace.displayName}`}
              onPress={() => {
                selectWorkspace(workspace.id);
                router.replace(`/(main)/workspace/${workspace.id}`);
                onNavigate();
              }}
              style={[styles.workspaceRow, isSelected && styles.workspaceRowSelected]}>
              <View style={[styles.stateDot, { backgroundColor: workspaceStateColor(workspace.state) }]} />
              <View style={styles.workspaceText}>
                <Text numberOfLines={1} style={styles.workspaceName}>{workspace.displayName}</Text>
                <Text numberOfLines={1} style={styles.workspaceBranch}>{workspace.branch}</Text>
              </View>
            </Pressable>
          );
        })}
        {workspaces.length === 0 &&
          <Text style={styles.empty}>No workspaces on this server yet.</Text>}
      </ScrollView>
      <View style={styles.serverFooter}>
        <Text style={styles.sectionLabel}>Server</Text>
        <Text numberOfLines={2} style={styles.serverUrl}>{activeServer?.url ?? 'Not connected'}</Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Connect to another server"
          onPress={() => router.push('/connect')}
          style={styles.footerButton}>
          <Text style={styles.footerButtonText}>Connect server</Text>
        </Pressable>
      </View>
    </View>
  );
}

export function WorkspaceSidebar() {
  const insets = useSafeAreaInsets();
  const { config, sidebarOpen, closeSidebar } = useAppShell();
  const sidebarBody: ReactNode = config.sidebarContent ?? <DefaultSidebarContent onNavigate={closeSidebar} />;

  return (
    <Modal visible={sidebarOpen} transparent animationType="fade" onRequestClose={closeSidebar}>
      <View style={styles.modal}>
        <Pressable accessibilityLabel="Close sidebar" style={styles.scrim} onPress={closeSidebar} />
        <View style={[styles.sidebar, { paddingTop: insets.top + 18, paddingBottom: insets.bottom + 18 }]}>
          {sidebarBody}
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  modal: { flex: 1, flexDirection: 'row' },
  scrim: { position: 'absolute', inset: 0, backgroundColor: agentUpTheme.colors.scrim },
  sidebar: {
    width: 280,
    height: '100%',
    paddingHorizontal: 16,
    backgroundColor: agentUpTheme.colors.surface,
    borderRightWidth: 1,
    borderRightColor: agentUpTheme.colors.borderSelected,
  },
  defaultContent: { flex: 1, gap: 12 },
  sectionLabel: { color: agentUpTheme.colors.textMuted, fontSize: 11, textTransform: 'uppercase', fontWeight: '700' },
  workspaceList: { gap: 8, paddingBottom: 12 },
  workspaceRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    padding: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSelected,
    backgroundColor: agentUpTheme.colors.surfaceRaised,
  },
  workspaceRowSelected: { borderColor: agentUpTheme.colors.accentSoft, backgroundColor: agentUpTheme.colors.surfaceSelected },
  stateDot: { width: 8, height: 8, borderRadius: 4 },
  workspaceText: { flex: 1, gap: 2 },
  workspaceName: { color: agentUpTheme.colors.textPrimary, fontWeight: '700' },
  workspaceBranch: { color: agentUpTheme.colors.textMuted, fontSize: 12 },
  empty: { color: agentUpTheme.colors.textMuted, lineHeight: 20 },
  serverFooter: { marginTop: 'auto', gap: 8, paddingTop: 12, borderTopWidth: 1, borderTopColor: agentUpTheme.colors.borderSelected },
  serverUrl: { color: agentUpTheme.colors.textPrimary, fontSize: 12, lineHeight: 16 },
  footerButton: {
    minHeight: 40,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSelected,
    backgroundColor: agentUpTheme.colors.surfaceRaised,
  },
  footerButtonText: { color: agentUpTheme.colors.accentSoft, fontWeight: '700' },
});
