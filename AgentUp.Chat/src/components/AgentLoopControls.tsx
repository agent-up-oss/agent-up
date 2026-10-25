import { Pressable, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { AgentLoopStatus, ServerAutonomy } from '../models/AgentSession';
import { canActivateLoop, canCancelLoop, canEnableAutonomy, preconditionLabel } from '../providers/LoopSurfaceProvider';

export type AgentLoopControlsProps = {
  autonomy: ServerAutonomy | null;
  loop: AgentLoopStatus | null | undefined;
  busy?: boolean;
  onEnable: () => void;
  onActivate: () => void;
  onCancel: () => void;
  onConfigurePolicy?: () => void;
};

export function AgentLoopControls({
  autonomy,
  loop,
  busy = false,
  onEnable,
  onActivate,
  onCancel,
  onConfigurePolicy,
}: AgentLoopControlsProps) {
  const items = autonomy?.preconditions ?? [];
  const serverItems = new Set(['forgeCapabilityEnabled', 'hostConnectionAuthorized', 'agentAvailable', 'autonomyEnabled']);
  const visibleItems = items.filter(item => serverItems.has(item.id) || !item.satisfied);
  if (items.length === 0 && !loop) return null;
  const enable = !busy && canEnableAutonomy(autonomy);
  const activate = !busy && canActivateLoop(autonomy, loop);
  const cancel = !busy && canCancelLoop(loop);
  const needsPolicy = items.some(item => item.id === 'permissionPolicy' && !item.satisfied);
  return (
    <View testID="agent-loop" style={styles.panel}>
      <Text style={styles.heading}>Unattended loop</Text>
      {visibleItems.length > 0 && (
        <View testID="agent-loop-checklist" style={styles.checklist}>
          {visibleItems.map(item => (
            <View key={item.id} testID={`agent-loop-precondition-${item.id}`} style={styles.row}>
              <Text style={styles.label}>{preconditionLabel(item.id)}</Text>
              <Text style={item.satisfied ? styles.ok : styles.muted}>
                {item.satisfied ? 'Ready' : item.remedy ?? 'Not ready'}
              </Text>
            </View>
          ))}
        </View>
      )}
      {loop?.phase ? <Text testID="agent-loop-phase" style={styles.muted}>{loop.phase}</Text> : null}
      {loop?.terminal ? <Text testID="agent-loop-terminal" style={styles.terminal}>{loop.terminal}</Text> : null}
      <View style={styles.actions}>
        {enable ? (
          <Pressable
            testID="agent-loop-enable"
            accessibilityRole="button"
            accessibilityLabel="Enable autonomy"
            onPress={onEnable}>
            <Text style={styles.action}>Enable autonomy</Text>
          </Pressable>
        ) : null}
        {needsPolicy && onConfigurePolicy ? (
          <Pressable
            testID="agent-loop-configure-policy"
            accessibilityRole="button"
            accessibilityLabel="Configure permissions"
            onPress={onConfigurePolicy}>
            <Text style={styles.action}>Configure permissions</Text>
          </Pressable>
        ) : null}
        {activate ? (
          <Pressable
            testID="agent-loop-activate"
            accessibilityRole="button"
            accessibilityLabel="Activate"
            onPress={onActivate}>
            <Text style={styles.action}>Activate</Text>
          </Pressable>
        ) : null}
        {cancel ? (
          <Pressable
            testID="agent-loop-cancel"
            accessibilityRole="button"
            accessibilityLabel="Cancel"
            onPress={onCancel}>
            <Text style={styles.action}>Cancel</Text>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  panel: { ...auBox('card'), gap: agentUpTheme.spacing[2] },
  heading: auText('fieldLabel'),
  checklist: { gap: 8 },
  row: { gap: 2 },
  label: auText('workspaceName'),
  ok: auText('accent'),
  muted: auText('muted'),
  terminal: { ...auText('workspaceName'), color: agentUpTheme.colors.accentSoft },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  action: auText('accent'),
});
