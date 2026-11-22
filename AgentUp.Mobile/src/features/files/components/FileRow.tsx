import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { WorkspaceFileEntry } from '../models/WorkspaceFiles';
import { fileSizeLabel, fileStatusClass, fileStatusGlyph } from '../providers/FileTreeProvider';

/**
 * One row of the Files tree. It is the Git change row without the checkbox: the same guides,
 * toggle, name and status glyph, plus a folder's changed-file count and a file's size.
 */
export function FileRow({
  entry,
  depth,
  expanded,
  loading,
  onToggle,
  onOpen,
}: {
  entry: WorkspaceFileEntry;
  depth: number;
  expanded: boolean;
  loading: boolean;
  onToggle: () => void;
  onOpen: () => void;
}) {
  const directory = entry.kind === 'Directory';
  const guides = Array.from({ length: depth }, (_, index) => index);
  const statusClass = fileStatusClass(entry.status);
  return (
    <View style={styles.row}>
      {guides.length > 0 &&
        <View style={styles.guides}>
          {guides.map(index => <View key={index} style={[styles.guide, auBox('gitTreeGuide')]} />)}
        </View>}
      {/* A folder is one control: the chevron and the name together unfold it. */}
      <Pressable
        testID={directory ? `files-toggle-${entry.path}` : `files-open-${entry.path}`}
        accessibilityRole="button"
        accessibilityLabel={directory ? `${expanded ? 'Collapse' : 'Expand'} ${entry.path}` : `Open ${entry.path}`}
        accessibilityState={directory ? { expanded } : undefined}
        onPress={directory ? onToggle : onOpen}
        style={styles.name}>
        {directory &&
          <View style={[styles.toggle, auBox('gitTreeToggle')]}>
            <Text style={[styles.center, auText('gitTreeToggle', expanded ? 'gitTreeToggleExpanded' : 'gitTreeToggleCollapsed')]}>
              {expanded ? '▾' : '▸'}
            </Text>
          </View>}
        <Text numberOfLines={1} style={[styles.label, auText(directory ? 'gitChangeNameDirectory' : 'gitChangeName')]}>{entry.name}</Text>
      </Pressable>
      {loading && <ActivityIndicator size="small" color={agentUpTheme.colors.textMuted} />}
      {directory && entry.changedFiles > 0 &&
        <Text accessibilityLabel={`${entry.changedFiles} changed`} style={[styles.count, auBox('filesCount'), auText('filesCount')]}>
          {entry.changedFiles}
        </Text>}
      {!directory && <Text style={auText('filesSize')}>{fileSizeLabel(entry.size)}</Text>}
      {!directory &&
        <Text style={[styles.center, styles.status, auBox('gitStatus'), auText('gitStatus', statusClass)]}>{fileStatusGlyph(entry.status)}</Text>}
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
  guides: { flexDirection: 'row', alignSelf: 'stretch' },
  guide: { alignSelf: 'stretch' },
  toggle: { alignItems: 'center', justifyContent: 'center' },
  center: { textAlign: 'center' },
  status: { minWidth: 14 },
  count: { textAlign: 'center', overflow: 'hidden', lineHeight: 18 },
  name: { flex: 1, minWidth: 0, flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  label: { flex: 1, minWidth: 0 },
});
