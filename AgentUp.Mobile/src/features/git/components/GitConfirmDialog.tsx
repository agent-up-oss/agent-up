import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { gitConfirmShowsCancel, type GitConfirmCopy } from '../providers/GitBranchPickerProvider';

export function GitConfirmDialog({
  copy,
  busy = false,
  onCancel,
  onConfirm,
}: {
  copy: GitConfirmCopy | null;
  busy?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  if (!copy) return null;
  const canCancel = gitConfirmShowsCancel(copy);

  return (
    <Modal
      visible
      transparent
      animationType="fade"
      onRequestClose={canCancel ? onCancel : onConfirm}>
      <View style={styles.scrim}>
        <View accessibilityRole="alert" style={styles.dialog}>
          <Text accessibilityRole="header" style={styles.title}>{copy.title}</Text>
          <ScrollView style={styles.body} keyboardShouldPersistTaps="handled">
            <Text style={styles.message}>{copy.message}</Text>
          </ScrollView>
          <View style={styles.actions}>
            {canCancel &&
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={typeof copy.cancel === 'string' ? copy.cancel : 'Cancel'}
                disabled={busy}
                onPress={onCancel}
                style={[styles.secondary, busy && styles.disabled]}>
                <Text style={styles.secondaryText}>{typeof copy.cancel === 'string' ? copy.cancel : 'Cancel'}</Text>
              </Pressable>}
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={copy.confirm}
              disabled={busy}
              onPress={onConfirm}
              style={[copy.destructive ? styles.danger : styles.primary, busy && styles.disabled]}>
              <Text style={copy.destructive ? styles.dangerText : styles.primaryText}>{copy.confirm}</Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  scrim: {
    flex: 1,
    padding: agentUpTheme.spacing[5],
    alignItems: 'center',
    justifyContent: 'center',
    ...auBox('scrim'),
  },
  dialog: { ...auBox('overlayPanel'), width: '100%', maxWidth: 480, gap: agentUpTheme.spacing[3], padding: agentUpTheme.spacing[4] },
  title: auText('pageTitle'),
  body: { maxHeight: 240 },
  message: auText('muted'),
  actions: { flexDirection: 'row', justifyContent: 'flex-end', flexWrap: 'wrap', gap: 10, marginTop: 6 },
  primary: { ...auBox('button', 'buttonCompact'), alignItems: 'center', justifyContent: 'center' },
  primaryText: auText('button', 'buttonCompact'),
  secondary: { ...auBox('button', 'buttonSecondary', 'buttonCompact'), alignItems: 'center', justifyContent: 'center' },
  secondaryText: auText('buttonSecondary', 'buttonCompact'),
  danger: { ...auBox('button', 'buttonDanger', 'buttonCompact'), alignItems: 'center', justifyContent: 'center' },
  dangerText: auText('button', 'buttonCompact'),
  disabled: { opacity: 0.38 },
});
