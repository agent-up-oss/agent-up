import { useRouter } from 'expo-router';
import { type ReactNode, useEffect, useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { CloneWorkspaceDialog, RefreshReviewDialog } from '@/features/workspaces/components/WorkspaceDialogs';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { isFeatureAvailable, workspaceCreateFeature } from '@/features/entitlements/models/Entitlements';
import { getEntitlements } from '@/features/entitlements/providers/EntitlementsApiProvider';
import { isDerivedWorkspace, workspaceSubtitle } from '@/features/workspaces/providers/WorkspaceLabelProvider';
import { orderWorkspaces } from '@/features/workspaces/providers/WorkspaceOrderProvider';
import { statusDotStyle, workspaceLedState } from '@/features/workspaces/providers/WorkspaceStatusProvider';
import { useAppShell } from '../controllers/AppShellContext';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

function DefaultSidebarContent({ onNavigate }: { onNavigate: () => void }) {
  const router = useRouter();
  const { activeServer, logout } = useServers();
  const { server, workspaces, selectedWorkspace, selectWorkspace, clone, refreshClone, remove } = useWorkspaces();
  const [canCreate, setCanCreate] = useState(true);
  const [adding, setAdding] = useState(false);
  const [repository, setRepository] = useState('');
  const [branch, setBranch] = useState('main');
  const [derived, setDerived] = useState(false);
  const [cloning, setCloning] = useState(false);
  const [cloneError, setCloneError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState<{ id: string; name: string; revision: string } | null>(null);
  const [refreshBusy, setRefreshBusy] = useState(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);
  const [removing, setRemoving] = useState<{ id: string; name: string } | null>(null);

  useEffect(() => {
    if (!server) {
      setCanCreate(true);
      return;
    }
    let cancelled = false;
    void getEntitlements(server).then(document => {
      if (!cancelled) setCanCreate(isFeatureAvailable(document, workspaceCreateFeature));
    });
    return () => { cancelled = true; };
  }, [server]);

  const openAdd = () => {
    setRepository('');
    setBranch('main');
    setDerived(false);
    setCloneError(null);
    setAdding(true);
  };

  const confirmClone = async () => {
    if (cloning) return;
    setCloning(true);
    setCloneError(null);
    try {
      const revision = branch.trim();
      const workspace = await clone({
        repository: repository.trim(),
        branch: revision,
        ...(derived ? { derived: true, revision } : {}),
      });
      setAdding(false);
      router.replace(`/(main)/workspace/${workspace.id}`);
      onNavigate();
    } catch (cause) {
      setCloneError(cause instanceof Error ? cause.message : 'Could not clone the repository.');
    } finally {
      setCloning(false);
    }
  };

  const confirmRefresh = async () => {
    if (!refreshing || refreshBusy) return;
    setRefreshBusy(true);
    setRefreshError(null);
    try {
      await refreshClone(refreshing.id, refreshing.revision);
      setRefreshing(null);
    } catch (cause) {
      setRefreshError(cause instanceof Error ? cause.message : 'Could not refresh the review workspace.');
    } finally {
      setRefreshBusy(false);
    }
  };

  const confirmRemove = async () => {
    if (!removing) return;
    const target = removing;
    setRemoving(null);
    try {
      await remove(target.id);
    } catch {
      setRemoving(target);
    }
  };

  return (
    <View style={styles.defaultContent}>
      <View style={styles.sectionHeader}>
        <Text style={styles.sectionLabel}>Workspaces</Text>
        {canCreate ? (
          <Pressable accessibilityRole="button" accessibilityLabel="Add workspace" onPress={openAdd} style={styles.addButton} hitSlop={12}>
            <Text style={styles.addIcon}>+</Text>
          </Pressable>
        ) : null}
      </View>
      <ScrollView contentContainerStyle={styles.workspaceList}>
        {orderWorkspaces(workspaces).map(workspace => {
          const isSelected = selectedWorkspace?.id === workspace.id;
          const derivedWorkspace = isDerivedWorkspace(workspace);
          return (
            <View key={workspace.id} style={[styles.workspaceRow, isSelected && styles.workspaceRowSelected]}>
              <Pressable
                testID={`workspace-${workspace.id}`}
                accessibilityRole="button"
                accessibilityState={{ selected: isSelected }}
                accessibilityLabel={`Open workspace ${workspace.displayName}`}
                onPress={() => {
                  selectWorkspace(workspace.id);
                  router.replace(`/(main)/workspace/${workspace.id}`);
                  onNavigate();
                }}
                style={styles.workspaceOpen}>
                <View style={statusDotStyle(workspaceLedState(workspace.state, workspace.healthState))} />
                <View style={styles.workspaceText}>
                  <Text numberOfLines={1} style={styles.workspaceName}>{workspace.displayName}</Text>
                  <Text numberOfLines={1} style={styles.workspaceBranch}>{workspaceSubtitle(workspace)}</Text>
                </View>
              </Pressable>
              {derivedWorkspace ? (
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={`Refresh review workspace ${workspace.displayName}`}
                  onPress={() => {
                    setRefreshError(null);
                    setRefreshing({
                      id: workspace.id,
                      name: workspace.displayName,
                      revision: workspaceSubtitle(workspace),
                    });
                  }}
                  hitSlop={8}
                  style={styles.rowAction}>
                  <Text style={styles.rowActionText}>↻</Text>
                </Pressable>
              ) : null}
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`Remove workspace ${workspace.displayName}`}
                onPress={() => setRemoving({ id: workspace.id, name: workspace.displayName })}
                hitSlop={8}
                style={styles.rowAction}>
                <Text style={styles.rowActionText}>✕</Text>
              </Pressable>
            </View>
          );
        })}
        {workspaces.length === 0 &&
          <Text style={styles.empty}>No workspaces on this server yet.</Text>}
      </ScrollView>
      <CloneWorkspaceDialog
        visible={adding}
        repository={repository}
        branch={branch}
        derived={derived}
        cloning={cloning}
        error={cloneError}
        onRepository={setRepository}
        onBranch={setBranch}
        onDerived={setDerived}
        onCancel={() => setAdding(false)}
        onConfirm={() => { void confirmClone(); }}
      />
      <RefreshReviewDialog
        visible={refreshing !== null}
        name={refreshing?.name ?? ''}
        revision={refreshing?.revision ?? ''}
        busy={refreshBusy}
        error={refreshError}
        onRevision={revision => setRefreshing(current => current ? { ...current, revision } : current)}
        onCancel={() => setRefreshing(null)}
        onConfirm={() => { void confirmRefresh(); }}
      />
      <Modal visible={removing !== null} transparent animationType="fade" onRequestClose={() => setRemoving(null)}>
        <View style={styles.confirmScrim}>
          <View style={styles.confirmCard}>
            <Text accessibilityRole="header" style={styles.workspaceName}>Remove workspace</Text>
            <Text style={styles.empty}>{`Remove ${removing?.name ?? 'this workspace'} from this server?`}</Text>
            <View style={styles.confirmActions}>
              <Pressable accessibilityRole="button" accessibilityLabel="Cancel" onPress={() => setRemoving(null)} style={styles.logoutButton}>
                <Text style={styles.logoutText}>Cancel</Text>
              </Pressable>
              <Pressable accessibilityRole="button" accessibilityLabel="Remove" onPress={() => { void confirmRemove(); }} style={styles.logoutButton}>
                <Text style={styles.logoutText}>Remove</Text>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>
      <View style={styles.serverFooter}>
        <Text style={styles.sectionLabel}>Server</Text>
        <Text
          accessibilityLabel={activeServer ? `Connected to ${activeServer.displayName ?? activeServer.url}` : 'Not connected'}
          numberOfLines={2}
          style={styles.serverUrl}>
          {activeServer?.displayName ?? activeServer?.url ?? 'Not connected'}
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Log out of this server"
          onPress={() => {
            logout();
            onNavigate();
            router.replace('/connect');
          }}
          style={styles.logoutButton}>
          <Text style={styles.logoutText}>Logout</Text>
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
    ...auBox('drawer'),
    width: 280,
    height: '100%',
    paddingHorizontal: agentUpTheme.spacing[4],
  },
  defaultContent: { flex: 1, gap: agentUpTheme.spacing[3] },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  sectionLabel: auText('fieldLabel'),
  addButton: { ...auBox('workspaceAdd'), alignItems: 'center', justifyContent: 'center' },
  addIcon: auText('workspaceAdd'),
  workspaceList: { gap: agentUpTheme.spacing[2], paddingBottom: agentUpTheme.spacing[3] },
  workspaceRow: {
    ...auBox('workspace'),
    flexDirection: 'row',
    alignItems: 'center',
    gap: agentUpTheme.spacing[2],
  },
  workspaceOpen: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  rowAction: { ...auBox('workspaceAdd'), width: 28, height: 28, alignItems: 'center', justifyContent: 'center' },
  rowActionText: auText('workspaceAdd'),
  confirmScrim: { flex: 1, padding: agentUpTheme.spacing[5], alignItems: 'center', justifyContent: 'center', ...auBox('scrim') },
  confirmCard: { ...auBox('card'), width: '100%', maxWidth: 360, gap: agentUpTheme.spacing[3] },
  confirmActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: agentUpTheme.spacing[2] },
  workspaceRowSelected: auBox('workspaceSelected'),
  workspaceText: { flex: 1, gap: 2 },
  workspaceName: auText('workspaceName'),
  workspaceBranch: auText('workspaceBranch'),
  empty: auText('muted'),
  serverFooter: {
    marginTop: 'auto',
    gap: agentUpTheme.spacing[2],
    paddingTop: agentUpTheme.spacing[3],
    borderTopWidth: 1,
    borderTopColor: agentUpTheme.colors.borderSubtle,
  },
  serverUrl: { ...auText('workspaceName'), fontSize: agentUpTheme.typography.sizeXs, lineHeight: 16 },
  logoutButton: {
    ...auBox('sessionLogout'),
    alignItems: 'center',
    justifyContent: 'center',
  },
  logoutText: auText('sessionLogout'),
});
