import { useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useWorkspaces } from '../controllers/WorkspacesContext';
import { canCloneWorkspace } from '../providers/CloneInputProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export function WorkspaceListScreen() {
  const { server, workspaces, selectedWorkspace, loading, error, selectWorkspace, refresh, clone } = useWorkspaces();
  const [adding, setAdding] = useState(false);
  const [repository, setRepository] = useState('');
  const [branch, setBranch] = useState('main');
  const [cloning, setCloning] = useState(false);
  const [cloneError, setCloneError] = useState<string | null>(null);

  const openDialog = () => { setRepository(''); setBranch('main'); setCloneError(null); setAdding(true); };

  const confirmClone = async () => {
    if (cloning) return;
    setCloning(true); setCloneError(null);
    try {
      await clone({ repository: repository.trim(), branch: branch.trim() });
      setAdding(false);
    } catch (cause) {
      setCloneError(cause instanceof Error ? cause.message : 'Could not clone the repository.');
    } finally {
      setCloning(false);
    }
  };

  const canConfirm = !cloning && canCloneWorkspace(repository, branch);

  return <SafeAreaView style={styles.screen}>
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <View style={styles.headerRow}>
        <Text accessibilityRole="header" style={styles.title}>Workspaces</Text>
        <Pressable accessibilityRole="button" accessibilityLabel="Add workspace" onPress={openDialog} style={styles.addButton} hitSlop={12}>
          <Text style={styles.addIcon}>+</Text>
        </Pressable>
      </View>
      <Text style={styles.subtitle}>
        {server ? `Connected to ${server.url}` : 'No server selected. Add one on the Servers tab.'}
      </Text>

      {loading && <ActivityIndicator color={agentUpTheme.colors.accent} />}
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}

      {workspaces.map(workspace => {
        const isSelected = selectedWorkspace?.id === workspace.id;
        return <Pressable key={workspace.id} accessibilityRole="button"
          accessibilityState={{ selected: isSelected }}
          accessibilityLabel={`Select workspace ${workspace.displayName}`}
          onPress={() => selectWorkspace(workspace.id)}
          style={[styles.row, isSelected && styles.selectedRow]}>
          <View style={styles.cardHeader}>
            <View style={stateDot(workspace.state)} />
            <Text numberOfLines={1} style={styles.cardTitle}>{workspace.displayName}</Text>
          </View>
          <Text numberOfLines={1} style={styles.cardBranch}>{workspace.branch}</Text>
        </Pressable>;
      })}

      {!loading && !error && workspaces.length === 0 &&
        <Text style={styles.empty}>No workspaces yet. Use + to clone a repository.</Text>}

      <Pressable accessibilityRole="button" accessibilityLabel="Reload workspaces" onPress={() => void refresh()} style={styles.secondaryButton}>
        <Text style={styles.secondaryButtonText}>Reload</Text>
      </Pressable>
    </ScrollView>

    <Modal visible={adding} transparent animationType="fade" onRequestClose={() => setAdding(false)}>
      <View style={styles.modalScrim}>
        <View style={styles.dialog}>
          <Text accessibilityRole="header" style={styles.dialogTitle}>Add workspace</Text>
          <Text style={styles.dialogDetail}>
            Agent-Up clones the repository into its managed source clones directory and registers the workspace.
          </Text>

          <Text style={styles.label}>Repository</Text>
          <TextInput accessibilityLabel="Repository" autoCapitalize="none" autoCorrect={false}
            placeholder="https://github.com/acme/widgets.git" placeholderTextColor={agentUpTheme.colors.textFaint}
            value={repository} onChangeText={setRepository} editable={!cloning} style={styles.input} />

          <Text style={styles.label}>Branch</Text>
          <TextInput accessibilityLabel="Branch" autoCapitalize="none" autoCorrect={false}
            placeholder="main" placeholderTextColor={agentUpTheme.colors.textFaint}
            value={branch} onChangeText={setBranch} editable={!cloning} style={styles.input} />

          {!!cloneError && <Text accessibilityRole="alert" style={styles.error}>{cloneError}</Text>}

          <View style={styles.dialogActions}>
            <Pressable accessibilityRole="button" accessibilityLabel="Cancel" disabled={cloning}
              onPress={() => setAdding(false)} style={[styles.secondaryButton, cloning && styles.disabled]}>
              <Text style={styles.secondaryButtonText}>Cancel</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel="Clone" disabled={!canConfirm}
              onPress={() => void confirmClone()} style={[styles.button, !canConfirm && styles.disabled]}>
              {cloning ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.buttonText}>Clone</Text>}
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  </SafeAreaView>;
}

function stateDot(state: string) {
  if (state === 'Running') return auBox('statusDot', 'statusDotHealthy');
  if (state === 'Starting' || state === 'Stopping') return auBox('statusDot', 'statusDotWarning');
  if (state === 'Failed') return auBox('statusDot', 'statusDotDanger');
  return auBox('statusDot');
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[5], paddingTop: 78, paddingBottom: agentUpTheme.spacing[8], gap: 14 },
  headerRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  title: auText('title'),
  addButton: { ...auBox('workspaceAdd'), alignItems: 'center', justifyContent: 'center' },
  addIcon: auText('workspaceAdd'),
  subtitle: auText('muted'),
  row: { ...auBox('workspace'), gap: 5 },
  selectedRow: auBox('workspaceSelected'),
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  cardTitle: auText('workspaceName'),
  cardBranch: auText('workspaceBranch'),
  empty: auText('muted'),
  error: auText('badgeDanger'),
  label: { ...auText('heading'), fontSize: agentUpTheme.typography.sizeSm },
  input: auBox('input'),
  button: { ...auBox('button'), alignItems: 'center', justifyContent: 'center' },
  buttonText: auText('button'),
  secondaryButton: { ...auBox('button', 'buttonSecondary'), alignItems: 'center', justifyContent: 'center' },
  secondaryButtonText: auText('buttonSecondary'),
  disabled: { opacity: 0.38 },
  modalScrim: { flex: 1, padding: agentUpTheme.spacing[5], alignItems: 'center', justifyContent: 'center', ...auBox('scrim') },
  dialog: { ...auBox('card'), width: '100%', maxWidth: 480, gap: 10 },
  dialogTitle: auText('heading'),
  dialogDetail: auText('muted'),
  dialogActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 10, marginTop: 6 },
});
