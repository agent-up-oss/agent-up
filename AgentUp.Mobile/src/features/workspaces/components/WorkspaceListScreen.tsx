import { useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useWorkspaces } from '../controllers/WorkspacesContext';
import { canCloneWorkspace } from '../providers/CloneInputProvider';
import { agentUpTheme } from '@agent-up/design-system/native';

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
        <Pressable accessibilityRole="button" accessibilityLabel="Add workspace" onPress={openDialog} style={styles.addButton}>
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
          style={[styles.card, isSelected && styles.selectedCard]}>
          <View style={styles.cardHeader}>
            <View style={[styles.stateDot, { backgroundColor: stateColor(workspace.state) }]} />
            <Text numberOfLines={1} style={styles.cardTitle}>{workspace.displayName}</Text>
          </View>
          <Text numberOfLines={1} style={styles.cardBranch}>{workspace.branch}</Text>
          <Text numberOfLines={1} style={styles.cardPath}>{workspace.worktreePath}</Text>
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
              {cloning ? <ActivityIndicator color={agentUpTheme.colors.canvas} /> : <Text style={styles.buttonText}>Clone</Text>}
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  </SafeAreaView>;
}

function stateColor(state: string): string {
  if (state === 'Running') return agentUpTheme.colors.accent;
  if (state === 'Starting' || state === 'Stopping') return agentUpTheme.colors.statusWarning;
  if (state === 'Failed') return agentUpTheme.colors.statusDanger;
  return agentUpTheme.colors.textFaint;
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: 20, paddingTop: 78, paddingBottom: 32, gap: 14 },
  headerRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  title: { color: agentUpTheme.colors.textPrimary, fontSize: 32, lineHeight: 36, fontWeight: '800' },
  addButton: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center', borderRadius: 8, borderWidth: 1, borderColor: agentUpTheme.colors.borderSelected, backgroundColor: agentUpTheme.colors.surface },
  addIcon: { color: agentUpTheme.colors.accentSoft, fontSize: 26, lineHeight: 30, fontWeight: '800' },
  subtitle: { color: agentUpTheme.colors.textMuted, fontSize: 14 },
  card: { padding: 16, borderRadius: 8, borderWidth: 1, borderColor: agentUpTheme.colors.borderSelected, backgroundColor: agentUpTheme.colors.surface, gap: 5 },
  selectedCard: { borderColor: agentUpTheme.colors.accentSoft, backgroundColor: agentUpTheme.colors.surfaceSelected },
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  stateDot: { width: 8, height: 8, borderRadius: 4 },
  cardTitle: { color: agentUpTheme.colors.textPrimary, fontSize: 17, fontWeight: '700', flexShrink: 1 },
  cardBranch: { color: agentUpTheme.colors.textMuted, fontSize: 13 },
  cardPath: { color: agentUpTheme.colors.textFaint, fontSize: 11 },
  empty: { color: agentUpTheme.colors.textMuted, lineHeight: 21 },
  error: { color: agentUpTheme.colors.statusDanger, lineHeight: 21 },
  label: { color: agentUpTheme.colors.textPrimary, fontWeight: '700' },
  input: { minHeight: 48, borderRadius: 8, borderWidth: 1, borderColor: agentUpTheme.colors.borderSelected, paddingHorizontal: 14, color: agentUpTheme.colors.textPrimary, backgroundColor: agentUpTheme.colors.surfaceRaised },
  button: { minHeight: 46, paddingHorizontal: 22, alignItems: 'center', justifyContent: 'center', borderRadius: 8, backgroundColor: agentUpTheme.colors.accent },
  buttonText: { color: agentUpTheme.colors.canvas, fontWeight: '800' },
  secondaryButton: { minHeight: 46, paddingHorizontal: 22, alignItems: 'center', justifyContent: 'center', borderRadius: 8, borderWidth: 1, borderColor: agentUpTheme.colors.borderSelected, backgroundColor: agentUpTheme.colors.surface },
  secondaryButtonText: { color: agentUpTheme.colors.textPrimary, fontWeight: '700' },
  disabled: { opacity: 0.38 },
  modalScrim: { flex: 1, padding: 20, alignItems: 'center', justifyContent: 'center', backgroundColor: agentUpTheme.colors.scrim },
  dialog: { width: '100%', maxWidth: 480, padding: 20, borderRadius: 10, borderWidth: 1, borderColor: agentUpTheme.colors.borderSelected, backgroundColor: agentUpTheme.colors.surface, gap: 10 },
  dialogTitle: { color: agentUpTheme.colors.textPrimary, fontSize: 22, fontWeight: '800' },
  dialogDetail: { color: agentUpTheme.colors.textMuted, lineHeight: 21 },
  dialogActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 10, marginTop: 6 },
});
