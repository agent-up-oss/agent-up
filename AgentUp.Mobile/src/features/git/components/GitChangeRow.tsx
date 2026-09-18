import { Pressable, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { GitChangeNode } from '../models/GitChanges';
import {
  directoryToggleClass,
  directoryToggleGlyph,
  nameClass,
  statusClass,
  statusGlyph,
} from '../providers/GitChangeTreeProvider';

export function GitChangeRow({
  node,
  checked,
  open,
  expanded,
  onToggle,
  onToggleExpand,
  onOpenFile,
}: {
  node: GitChangeNode;
  checked: boolean;
  open: boolean;
  expanded: boolean;
  onToggle: () => void;
  onToggleExpand: () => void;
  onOpenFile: () => void;
}) {
  const guides = Array.from({ length: node.depth }, (_, index) => index);
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
      {guides.length > 0 &&
        <View style={styles.guides}>
          {guides.map(index =>
            <View key={index} style={[styles.guide, auBox('gitTreeGuide')]} />)}
        </View>}
      {node.isDirectory &&
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`${expanded ? 'Collapse' : 'Expand'} ${node.path || node.name}`}
          onPress={onToggleExpand}
          style={[styles.toggle, auBox('gitTreeToggle')]}>
          <Text style={[styles.status, auText('gitTreeToggle', directoryToggleClass(expanded))]}>
            {directoryToggleGlyph(expanded)}
          </Text>
        </Pressable>}
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
  guides: { flexDirection: 'row', alignSelf: 'stretch' },
  guide: { alignSelf: 'stretch' },
  toggle: { alignItems: 'center', justifyContent: 'center' },
  status: { textAlign: 'center' },
  nameButton: { flex: 1, minWidth: 0 },
});
