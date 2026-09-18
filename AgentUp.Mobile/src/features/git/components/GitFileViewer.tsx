import { useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  type ListRenderItemInfo,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { SyntaxToken } from '@agent-up/design-system/syntax';
import type { GitFileDiff } from '../models/GitChanges';
import {
  FILE_VIEWER_LINE_HEIGHT,
  FILE_VIEWER_WINDOW,
  fileViewerHunks,
  fileViewerMessage,
  highlightLine,
  jumpIndex,
  lineBoxNames,
  parseFileDiff,
  prefixClassName,
  syntaxClassName,
  type FileViewerLine,
} from '../providers/GitFileViewerProvider';

const tokenCache = new WeakMap<FileViewerLine, SyntaxToken[]>();

export function GitFileViewer({
  path,
  status,
  diff,
  loading,
  onClose,
}: {
  path: string;
  status: string;
  diff: GitFileDiff | null;
  loading: boolean;
  onClose: () => void;
}) {
  const list = useRef<FlatList<FileViewerLine>>(null);
  const [current, setCurrent] = useState(0);
  const [goto, setGoto] = useState('');
  const message = fileViewerMessage(diff, path);
  const lines = useMemo(() => (message || !diff ? [] : parseFileDiff(diff.diff)), [diff, message]);
  const hunks = useMemo(() => fileViewerHunks(lines), [lines]);

  const jump = (index: number) => {
    setCurrent(index);
    list.current?.scrollToIndex({ index, animated: true, viewPosition: 0.15 });
  };

  const renderLine = ({ item }: ListRenderItemInfo<FileViewerLine>) => {
    const tokens = tokensFor(path, item);
    return (
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Line ${item.newNumber ?? item.oldNumber ?? item.index + 1}`}
        onPress={() => setCurrent(item.index)}
        style={[styles.line, auBox(...lineBoxNames(item.kind, item.index === current))]}>
        <Text style={[styles.gutter, auText('fileViewerGutter')]}>{item.newNumber ?? item.oldNumber ?? ''}</Text>
        <Text style={auText(prefixClassName(item.kind))}>{item.prefix}</Text>
        <Text style={[styles.code, auText('fileViewerCode')]} numberOfLines={1}>
          {tokens.map((token, tokenIndex) => (
            <Text key={`${item.index}:${tokenIndex}`} style={auText(syntaxClassName(token.kind))}>{token.text}</Text>
          ))}
        </Text>
      </Pressable>
    );
  };

  return (
    <View testID="git-file-viewer" accessibilityLabel="File viewer" style={[styles.viewer, auBox('fileViewer')]}>
      <View style={[styles.header, auBox('fileViewerHeader')]}>
        <View style={styles.heading}>
          <Text testID="git-file-viewer-path" accessibilityRole="header" numberOfLines={2} style={auText('fileViewerPath')}>{path}</Text>
          {!!status && <Text style={auText('fileViewerStatus')}>{status}</Text>}
        </View>
        <Pressable
          testID="git-file-viewer-close"
          accessibilityRole="button"
          accessibilityLabel="Close file viewer"
          onPress={onClose}
          style={styles.close}>
          <Text style={auText('buttonSecondary', 'buttonCompact')}>Close</Text>
        </Pressable>
      </View>

      {hunks.length > 0 &&
        <View style={[styles.nav, auBox('fileViewerNav')]}>
          <ScrollView horizontal contentContainerStyle={styles.navRow} keyboardShouldPersistTaps="handled">
            {hunks.map(hunk =>
              <Pressable
                key={hunk.lineIndex}
                accessibilityRole="button"
                accessibilityLabel={`Jump to ${hunk.label}`}
                onPress={() => jump(hunk.lineIndex)}
                style={[styles.jump, auBox('fileViewerJump')]}>
                <Text numberOfLines={1} style={auText('fileViewerJump')}>{hunk.label}</Text>
              </Pressable>)}
          </ScrollView>
          <TextInput
            accessibilityLabel="Go to line"
            keyboardType="number-pad"
            value={goto}
            onChangeText={setGoto}
            onSubmitEditing={() => {
              const index = jumpIndex(lines, goto);
              if (index != null) jump(index);
            }}
            placeholder="Line"
            placeholderTextColor={agentUpTheme.colors.textFaint}
            style={[styles.goto, auBox('fileViewerGoto'), auText('fileViewerGoto')]}
          />
        </View>}

      {loading
        ? <ActivityIndicator color={agentUpTheme.colors.accentSoft} style={styles.loading} />
        : message
          ? <Text style={[styles.message, auText('muted')]}>{message}</Text>
          : <FlatList
              ref={list}
              testID="git-file-viewer-lines"
              data={lines}
              keyExtractor={item => String(item.index)}
              renderItem={renderLine}
              getItemLayout={(_, index) => ({
                length: FILE_VIEWER_LINE_HEIGHT,
                offset: FILE_VIEWER_LINE_HEIGHT * index,
                index,
              })}
              initialNumToRender={FILE_VIEWER_WINDOW}
              maxToRenderPerBatch={24}
              windowSize={9}
              onScrollToIndexFailed={({ index }) => {
                setTimeout(() => list.current?.scrollToIndex({ index, animated: false }), 16);
              }}
              style={auBox('fileViewerBody')}
            />}
    </View>
  );
}

function tokensFor(path: string, line: FileViewerLine): SyntaxToken[] {
  const cached = tokenCache.get(line);
  if (cached) return cached;
  const tokens = highlightLine(path, line);
  tokenCache.set(line, tokens);
  return tokens;
}

const styles = StyleSheet.create({
  viewer: { width: '100%', maxWidth: 720, maxHeight: '90%', overflow: 'hidden' },
  header: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  heading: { flex: 1, minWidth: 0, gap: 2 },
  close: { ...auBox('button', 'buttonSecondary', 'buttonCompact'), alignItems: 'center', justifyContent: 'center' },
  nav: { gap: 8 },
  navRow: { alignItems: 'center', gap: 8, paddingRight: 8 },
  jump: { maxWidth: 220, alignItems: 'center', justifyContent: 'center' },
  goto: { minWidth: 72 },
  loading: { marginVertical: 24 },
  message: { padding: 16, lineHeight: 21 },
  line: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  gutter: { width: 40, textAlign: 'right' },
  code: { flex: 1, minWidth: 0 },
});
