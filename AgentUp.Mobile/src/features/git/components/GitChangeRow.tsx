import { Pressable, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { GitChangeNode } from '../models/GitChanges';
import { nameClass, statusClass, statusGlyph } from '../providers/GitChangeTreeProvider';

export function GitChangeRow({
  node,
  checked,
  open,
  onToggle,
  onOpenFile,
}: {
  node: GitChangeNode;
  checked: boolean;
  open: boolean;
  onToggle: () => void;
  onOpenFile: () => void;
}) {
  return (
    <View style={[styles.row, open && styles.rowOpen]}>
      <Pressable
        accessibilityRole="checkbox"
        accessibilityState={{ checked }}
        accessibilityLabel={`Select ${node.path || node.name}`}
        onPress={onToggle}
        style={[styles.checkbox, checked && styles.checkboxChecked]}>
        <Text style={styles.checkmark}>{checked ? '✓' : ''}</Text>
      </Pressable>
      <View style={{ width: node.depth * agentUpTheme.spacing[3] }} />
      {node.isDirectory &&
        <Text style={[styles.status, auBox('gitStatus'), auText(statusClass(null))]}>{statusGlyph(null)}</Text>}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Open ${node.path || node.name}`}
        disabled={node.isDirectory}
        onPress={onOpenFile}
        style={styles.nameButton}>
        <Text numberOfLines={1} style={auText(nameClass(node.isDirectory))}>{node.name}</Text>
      </Pressable>
      {!node.isDirectory &&
        <Text style={[styles.status, auBox('gitStatus'), auText(statusClass(node.status))]}>{statusGlyph(node.status)}</Text>}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    ...auBox('gitRow'),
    flexDirection: 'row',
    alignItems: 'center',
    gap: agentUpTheme.spacing[2],
  },
  rowOpen: auBox('gitRowSelected'),
  checkbox: { ...auBox('checkbox'), alignItems: 'center', justifyContent: 'center' },
  checkboxChecked: auBox('checkboxChecked'),
  checkmark: { ...auText('workspaceName'), fontSize: 12, lineHeight: 14 },
  status: { textAlign: 'center' },
  nameButton: { flex: 1, minWidth: 0 },
});
