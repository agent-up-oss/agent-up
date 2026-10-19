import { ActivityIndicator, Modal, Pressable, Text, TextInput, View, StyleSheet } from 'react-native';
import { canCloneWorkspace } from '../providers/CloneInputProvider';
import { cloneConfirmLabel, cloneRevisionLabel } from '../providers/WorkspaceLabelProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

type CloneDialogProps = {
  visible: boolean;
  repository: string;
  branch: string;
  derived: boolean;
  cloning: boolean;
  error: string | null;
  onRepository: (value: string) => void;
  onBranch: (value: string) => void;
  onDerived: (value: boolean) => void;
  onCancel: () => void;
  onConfirm: () => void;
};

export function CloneWorkspaceDialog({
  visible, repository, branch, derived, cloning, error,
  onRepository, onBranch, onDerived, onCancel, onConfirm,
}: CloneDialogProps) {
  const canConfirm = !cloning && canCloneWorkspace(repository, branch);
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onCancel}>
      <View style={styles.modalScrim}>
        <View style={styles.dialog}>
          <Text accessibilityRole="header" style={styles.dialogTitle}>Add workspace</Text>
          <Text style={styles.dialogDetail}>
            Agent-Up clones the repository into its managed source clones directory and registers the workspace.
          </Text>
          <Text style={styles.label}>Repository</Text>
          <TextInput accessibilityLabel="Repository" autoCapitalize="none" autoCorrect={false}
            placeholder="https://github.com/acme/widgets.git" placeholderTextColor={agentUpTheme.colors.textFaint}
            value={repository} onChangeText={onRepository} editable={!cloning} style={styles.input} />
          <Text style={styles.label}>{cloneRevisionLabel(derived)}</Text>
          <TextInput accessibilityLabel={cloneRevisionLabel(derived)} autoCapitalize="none" autoCorrect={false}
            placeholder="main" placeholderTextColor={agentUpTheme.colors.textFaint}
            value={branch} onChangeText={onBranch} editable={!cloning} style={styles.input} />
          <Pressable
            accessibilityRole="checkbox"
            accessibilityState={{ checked: derived }}
            accessibilityLabel="Start a review workspace at this revision"
            onPress={() => onDerived(!derived)}
            style={styles.checkboxRow}>
            <View style={[styles.checkbox, derived && styles.checkboxChecked]} />
            <Text style={styles.checkboxLabel}>Start a review workspace at this revision</Text>
          </Pressable>
          {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
          <View style={styles.dialogActions}>
            <Pressable accessibilityRole="button" accessibilityLabel="Cancel" disabled={cloning}
              onPress={onCancel} style={[styles.secondaryButton, cloning && styles.disabled]}>
              <Text style={styles.secondaryButtonText}>Cancel</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel={cloneConfirmLabel(derived, false)} disabled={!canConfirm}
              onPress={onConfirm} style={[styles.button, !canConfirm && styles.disabled]}>
              {cloning
                ? <ActivityIndicator color={agentUpTheme.colors.onAccent} />
                : <Text style={styles.buttonText}>{cloneConfirmLabel(derived, cloning)}</Text>}
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

type RefreshDialogProps = {
  visible: boolean;
  name: string;
  revision: string;
  busy: boolean;
  error: string | null;
  onRevision: (value: string) => void;
  onCancel: () => void;
  onConfirm: () => void;
};

export function RefreshReviewDialog({
  visible, name, revision, busy, error, onRevision, onCancel, onConfirm,
}: RefreshDialogProps) {
  const canConfirm = !busy && revision.trim().length > 0;
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onCancel}>
      <View style={styles.modalScrim}>
        <View style={styles.dialog}>
          <Text accessibilityRole="header" style={styles.dialogTitle}>Refresh review workspace</Text>
          <Text style={styles.dialogDetail}>
            {`Fetches this revision into ${name}. Your other checkouts stay where they are.`}
          </Text>
          <Text style={styles.label}>Revision</Text>
          <TextInput accessibilityLabel="Revision" autoCapitalize="none" autoCorrect={false}
            placeholder="branch, tag, or commit" placeholderTextColor={agentUpTheme.colors.textFaint}
            value={revision} onChangeText={onRevision} editable={!busy} style={styles.input} />
          {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
          <View style={styles.dialogActions}>
            <Pressable accessibilityRole="button" accessibilityLabel="Cancel" disabled={busy}
              onPress={onCancel} style={[styles.secondaryButton, busy && styles.disabled]}>
              <Text style={styles.secondaryButtonText}>Cancel</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel="Refresh" disabled={!canConfirm}
              onPress={onConfirm} style={[styles.button, !canConfirm && styles.disabled]}>
              {busy
                ? <ActivityIndicator color={agentUpTheme.colors.onAccent} />
                : <Text style={styles.buttonText}>Refresh</Text>}
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  label: auText('fieldLabel'),
  input: { ...auBox('input'), ...auText('input') },
  button: { ...auBox('button'), alignItems: 'center', justifyContent: 'center' },
  buttonText: auText('button'),
  secondaryButton: { ...auBox('button', 'buttonSecondary'), alignItems: 'center', justifyContent: 'center' },
  secondaryButtonText: auText('buttonSecondary'),
  disabled: auBox('buttonDisabled'),
  error: auText('badgeDanger'),
  modalScrim: { flex: 1, padding: agentUpTheme.spacing[5], alignItems: 'center', justifyContent: 'center', ...auBox('scrim') },
  dialog: { ...auBox('card'), width: '100%', maxWidth: 480, gap: agentUpTheme.spacing[3] },
  dialogTitle: auText('pageTitle'),
  dialogDetail: auText('muted'),
  dialogActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 10, marginTop: 6 },
  checkboxRow: { flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[3] },
  checkbox: { ...auBox('input'), width: 18, height: 18, padding: 0 },
  checkboxChecked: auBox('workspaceSelected'),
  checkboxLabel: auText('muted'),
});
