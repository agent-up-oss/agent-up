import { useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useWorkspaces } from '../controllers/WorkspacesContext';

export function WorkspaceListScreen() {
  const { serverUrl, workspaces, selectedWorkspace, loading, error, selectWorkspace, refresh, clone } = useWorkspaces();
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

  const canConfirm = !cloning && repository.trim().length > 0 && branch.trim().length > 0;

  return <SafeAreaView style={styles.screen}>
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <View style={styles.headerRow}>
        <Text accessibilityRole="header" style={styles.title}>Workspaces</Text>
        <Pressable accessibilityRole="button" accessibilityLabel="Add workspace" onPress={openDialog} style={styles.addButton}>
          <Text style={styles.addIcon}>+</Text>
        </Pressable>
      </View>
      <Text style={styles.subtitle}>
        {serverUrl ? `Connected to ${serverUrl}` : 'No server selected. Add one on the Servers tab.'}
      </Text>

      {loading && <ActivityIndicator color="#00d66b" />}
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
            placeholder="https://github.com/acme/widgets.git" placeholderTextColor="#718077"
            value={repository} onChangeText={setRepository} editable={!cloning} style={styles.input} />

          <Text style={styles.label}>Branch</Text>
          <TextInput accessibilityLabel="Branch" autoCapitalize="none" autoCorrect={false}
            placeholder="main" placeholderTextColor="#718077"
            value={branch} onChangeText={setBranch} editable={!cloning} style={styles.input} />

          {!!cloneError && <Text accessibilityRole="alert" style={styles.error}>{cloneError}</Text>}

          <View style={styles.dialogActions}>
            <Pressable accessibilityRole="button" accessibilityLabel="Cancel" disabled={cloning}
              onPress={() => setAdding(false)} style={[styles.secondaryButton, cloning && styles.disabled]}>
              <Text style={styles.secondaryButtonText}>Cancel</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel="Clone" disabled={!canConfirm}
              onPress={() => void confirmClone()} style={[styles.button, !canConfirm && styles.disabled]}>
              {cloning ? <ActivityIndicator color="#000000" /> : <Text style={styles.buttonText}>Clone</Text>}
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  </SafeAreaView>;
}

function stateColor(state: string): string {
  if (state === 'Running') return '#00d66b';
  if (state === 'Starting' || state === 'Stopping') return '#e0a33c';
  if (state === 'Failed') return '#d84f4f';
  return '#718077';
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#000000' },
  content: { padding: 20, paddingTop: 78, paddingBottom: 32, gap: 14 },
  headerRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  title: { color: '#f5fbf7', fontSize: 32, lineHeight: 36, fontWeight: '800' },
  addButton: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center', borderRadius: 8, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505' },
  addIcon: { color: '#2bf27a', fontSize: 26, lineHeight: 30, fontWeight: '800' },
  subtitle: { color: '#aebcb3', fontSize: 14 },
  card: { padding: 16, borderRadius: 8, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505', gap: 5 },
  selectedCard: { borderColor: '#2bf27a', backgroundColor: '#08150d' },
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  stateDot: { width: 8, height: 8, borderRadius: 4 },
  cardTitle: { color: '#f5fbf7', fontSize: 17, fontWeight: '700', flexShrink: 1 },
  cardBranch: { color: '#9fb2a8', fontSize: 13 },
  cardPath: { color: '#718077', fontSize: 11 },
  empty: { color: '#aebcb3', lineHeight: 21 },
  error: { color: '#d84f4f', lineHeight: 21 },
  label: { color: '#f5fbf7', fontWeight: '700' },
  input: { minHeight: 48, borderRadius: 8, borderWidth: 1, borderColor: '#287038', paddingHorizontal: 14, color: '#f5fbf7', backgroundColor: '#080808' },
  button: { minHeight: 46, paddingHorizontal: 22, alignItems: 'center', justifyContent: 'center', borderRadius: 8, backgroundColor: '#00d66b' },
  buttonText: { color: '#000000', fontWeight: '800' },
  secondaryButton: { minHeight: 46, paddingHorizontal: 22, alignItems: 'center', justifyContent: 'center', borderRadius: 8, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505' },
  secondaryButtonText: { color: '#f5fbf7', fontWeight: '700' },
  disabled: { opacity: 0.38 },
  modalScrim: { flex: 1, padding: 20, alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.72)' },
  dialog: { width: '100%', maxWidth: 480, padding: 20, borderRadius: 10, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505', gap: 10 },
  dialogTitle: { color: '#f5fbf7', fontSize: 22, fontWeight: '800' },
  dialogDetail: { color: '#aebcb3', lineHeight: 21 },
  dialogActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 10, marginTop: 6 },
});
