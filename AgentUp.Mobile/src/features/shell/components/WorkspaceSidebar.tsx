import { useRouter } from 'expo-router';
import { type ReactNode } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { useAppShell } from '../controllers/AppShellContext';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

function workspaceDot(state: string) {
  if (state === 'Running') return auBox('statusDot', 'statusDotHealthy');
  if (state === 'Starting' || state === 'Stopping') return auBox('statusDot', 'statusDotWarning');
  if (state === 'Failed') return auBox('statusDot', 'statusDotDanger');
  return auBox('statusDot');
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
              <View style={workspaceDot(workspace.state)} />
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
  scrim: { position: 'absolute', inset: 0, ...auBox('scrim') },
  sidebar: {
    ...auBox('rail'),
    width: 280,
    height: '100%',
    paddingHorizontal: agentUpTheme.spacing[4],
  },
  defaultContent: { flex: 1, gap: agentUpTheme.spacing[3] },
  sectionLabel: auText('eyebrow'),
  workspaceList: { gap: agentUpTheme.spacing[2], paddingBottom: agentUpTheme.spacing[3] },
  workspaceRow: {
    ...auBox('workspace'),
    flexDirection: 'row',
    alignItems: 'center',
    gap: agentUpTheme.spacing[2],
  },
  workspaceRowSelected: auBox('workspaceSelected'),
  workspaceText: { flex: 1, gap: 2 },
  workspaceName: auText('workspaceName'),
  workspaceBranch: auText('workspaceBranch'),
  empty: auText('muted'),
  serverFooter: {
    marginTop: 'auto',
    gap: agentUpTheme.spacing[2],
    paddingTop: agentUpTheme.spacing[3],
    ...auBox('divider'),
    height: undefined,
  },
  serverUrl: { ...auText('muted'), fontSize: agentUpTheme.typography.sizeXs },
  footerButton: {
    ...auBox('button', 'buttonSecondary'),
    alignItems: 'center',
    justifyContent: 'center',
  },
  footerButtonText: auText('buttonSecondary'),
});
