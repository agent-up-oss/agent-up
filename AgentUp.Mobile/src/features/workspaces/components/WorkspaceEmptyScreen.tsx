import { useMemo, useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaces } from '../controllers/WorkspacesContext';
import { canCloneWorkspace } from '../providers/CloneInputProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

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
        {loading && <ActivityIndicator color={agentUpTheme.colors.accent} />}
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
    </>
  );
}

const styles = StyleSheet.create({
  content: { padding: agentUpTheme.spacing[5], paddingBottom: agentUpTheme.spacing[8], gap: 14 },
  subtitle: auText('muted'),
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
