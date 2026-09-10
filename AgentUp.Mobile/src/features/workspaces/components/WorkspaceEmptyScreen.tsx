import { useMemo, useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaces } from '../controllers/WorkspacesContext';
import { canCloneWorkspace } from '../providers/CloneInputProvider';

export function WorkspaceEmptyScreen() {
  const { server, loading, error, refresh, clone } = useWorkspaces();
  const [adding, setAdding] = useState(false);
  const [repository, setRepository] = useState('');
  const [branch, setBranch] = useState('main');
  const [cloning, setCloning] = useState(false);
  const [cloneError, setCloneError] = useState<string | null>(null);

  const shellConfig = useMemo(() => ({
    title: 'Workspaces',
    rightAction: {
      label: 'Add',
      accessibilityLabel: 'Add workspace',
      onPress: () => { setRepository(''); setBranch('main'); setCloneError(null); setAdding(true); },
    },
    sidebarContent: null,
  }), []);

  useShellConfig(shellConfig);

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

  return (
    <>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <Text style={styles.subtitle}>
          {server ? `Connected to ${server.url}` : 'Connect to a server to manage workspaces.'}
        </Text>
        {loading && <ActivityIndicator color="#00d66b" />}
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
        <Text style={styles.empty}>No workspaces on this server yet. Clone a repository to get started.</Text>
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
    </>
  );
}

const styles = StyleSheet.create({
  content: { padding: 20, paddingBottom: 32, gap: 14 },
  subtitle: { color: '#aebcb3', fontSize: 14 },
  empty: { color: '#aebcb3', lineHeight: 22 },
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
