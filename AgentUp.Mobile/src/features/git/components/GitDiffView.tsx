import { memo, useMemo, useState } from 'react';
import { ActivityIndicator, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { detectLanguage, tokenizeLine } from '@agent-up/design-system/syntax';
import { SheetDockMark } from '@/features/shell/components/SheetLayer';
import type { GitFileDiff } from '../models/GitChanges';
import {
  DIFF_FOLD_HEIGHT,
  DIFF_LINE_HEIGHT,
  diffFileLabel,
  diffLineNumber,
  diffMessage,
  diffSegments,
  paintedPieces,
  parseUnifiedDiff,
  revealMore,
  syntaxClassName,
  type DiffFold,
  type DiffLine,
  type DiffReveal,
} from '../providers/GitDiffProvider';

const MONO = Platform.select({ ios: 'Menlo', android: 'monospace', default: 'ui-monospace, SFMono-Regular, Menlo, monospace' });

/**
 * One changed file read in place: the code on the canvas with its line numbers, removed lines
 * washed red and added lines green, the words an edited line changed marked a step stronger, and
 * the unchanged stretches folded into rows that open five lines at a time. `file` is the file as it
 * is now, which is what a fold opens onto; without it the folds only count what they hide.
 *
 * In a sheet the head is the grabber over the file bar and closes it; in a page the file bar leads
 * with a back chevron.
 */
export function GitDiffView({ path, diff, file, loading, mode, onClose }: {
  path: string;
  diff: GitFileDiff | null;
  file: string[] | null;
  loading: boolean;
  mode: 'sheet' | 'page';
  onClose: () => void;
}) {
  const [reveal, setReveal] = useState<DiffReveal>({});
  const label = diffFileLabel(path);
  const message = loading ? null : diffMessage(diff, path);
  const parsed = useMemo(() => parseUnifiedDiff(message ? null : diff?.diff), [diff, message]);
  const segments = useMemo(() => diffSegments(parsed, file, reveal), [parsed, file, reveal]);
  const language = useMemo(() => detectLanguage(path), [path]);

  const open = (fold: DiffFold, direction: 'down' | 'up' | 'all') => setReveal(current => revealMore(current, fold, direction));

  return (
    <View testID="git-diff-view" style={styles.view}>
      {mode === 'sheet' ? (
        <Pressable
          testID="git-diff-view-grabber"
          accessibilityRole="button"
          accessibilityLabel="Close changes"
          onPress={onClose}
          style={styles.grabberRow}>
          <View style={styles.grabber} />
        </Pressable>
      ) : null}
      <View style={styles.file}>
        {mode === 'page' ? (
          <Pressable testID="git-diff-view-back" accessibilityRole="button" accessibilityLabel="Back to changes" onPress={onClose} style={styles.back}>
            <Text style={styles.backGlyph}>‹</Text>
          </Pressable>
        ) : null}
        <Text style={styles.kind}>{label.kind}</Text>
        <View style={styles.names}>
          <Text testID="git-diff-view-path" accessibilityRole="header" numberOfLines={1} style={styles.name}>{label.name}</Text>
          {label.folder ? <Text numberOfLines={1} style={styles.folder}>{label.folder}</Text> : null}
        </View>
        {parsed.added > 0 ? <Text style={styles.countAdded}>+{parsed.added}</Text> : null}
        {parsed.deleted > 0 ? <Text style={styles.countDeleted}>−{parsed.deleted}</Text> : null}
      </View>
      <SheetDockMark />

      {loading ? <ActivityIndicator color={agentUpTheme.colors.accentSoft} style={styles.loading} /> : null}
      {message ? <Text style={styles.note}>{message}</Text> : null}
      {!loading && !message ? (
        <ScrollView
          testID="git-diff-view-lines"
          style={styles.body}
          contentContainerStyle={styles.bodyContent}
>
          {segments.map(segment => segment.kind === 'fold'
            ? <Fold key={segment.fold.id} fold={segment.fold} onOpen={direction => open(segment.fold, direction)} />
            : <Block key={`block-${diffLineNumber(segment.lines[0])}-${segment.lines[0].kind}`} lines={segment.lines} language={language} />)}
        </ScrollView>
      ) : null}
    </View>
  );
}

/** A run of shown lines: numbers fixed on the left, the code scrolling sideways under them. */
const Block = memo(function Block({ lines, language }: { lines: DiffLine[]; language: string }) {
  return (
    <View style={styles.block}>
      <View>
        {lines.map((line, index) => (
          <Text
            key={index}
            style={[
              styles.number,
              line.kind === 'added' && styles.numberAdded,
              line.kind === 'deleted' && styles.numberDeleted,
            ]}>
            {diffLineNumber(line) ?? ''}
          </Text>
        ))}
      </View>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.codeScroll} contentContainerStyle={styles.codeColumn}>
        <View style={styles.codeColumn}>
          {lines.map((line, index) => <CodeLine key={index} line={line} language={language} />)}
        </View>
      </ScrollView>
    </View>
  );
});

function CodeLine({ line, language }: { line: DiffLine; language: string }) {
  const pieces = useMemo(() => paintedPieces(tokenizeLine(line.text, language), line.words), [line, language]);
  const changedWord = line.kind === 'added' ? styles.wordAdded : styles.wordDeleted;
  return (
    <View
      accessibilityLabel={`${line.kind === 'context' ? 'Line' : line.kind === 'added' ? 'Added line' : 'Removed line'} ${diffLineNumber(line) ?? ''}`}
      style={[styles.line, line.kind === 'added' && styles.lineAdded, line.kind === 'deleted' && styles.lineDeleted]}>
      <Text style={styles.code}>
        {pieces.length ? pieces.map((piece, index) => (
          <Text key={index} style={[auText(syntaxClassName(piece.kind)), piece.changed && changedWord]}>{piece.text}</Text>
        )) : ' '}
      </Text>
    </View>
  );
}

/** Unchanged lines folded away: how many, and five more either way or all of them. */
function Fold({ fold, onOpen }: { fold: DiffFold; onOpen: (direction: 'down' | 'up' | 'all') => void }) {
  const opens = fold.opensDown || fold.opensUp;
  const small = fold.hidden <= 5;
  return (
    <View testID={`git-diff-view-${fold.id}`} style={styles.fold}>
      <Text style={styles.foldRule}>⋯</Text>
      <Text style={styles.foldLabel}>{fold.hidden === 1 ? '1 unmodified line' : `${fold.hidden} unmodified lines`}</Text>
      {opens && small ? <Expand label={`Show ${fold.hidden}`} onPress={() => onOpen('all')} /> : null}
      {opens && !small && fold.opensDown ? <Expand label="↓ 5" hint="Show 5 more lines below the change above" onPress={() => onOpen('down')} /> : null}
      {opens && !small && fold.opensUp ? <Expand label="↑ 5" hint="Show 5 more lines above the change below" onPress={() => onOpen('up')} /> : null}
      {opens && !small ? <Expand label="All" hint={`Show all ${fold.hidden} lines`} onPress={() => onOpen('all')} /> : null}
    </View>
  );
}

function Expand({ label, hint, onPress }: { label: string; hint?: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={hint ?? label} hitSlop={6} onPress={onPress} style={styles.expand}>
      <Text style={styles.expandText}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  view: { ...auBox('diffView'), flex: 1 },
  grabberRow: { ...auBox('sheetGrabberRow'), alignItems: 'center' },
  grabber: auBox('sheetGrabber'),
  file: { ...auBox('diffFile'), flexDirection: 'row', alignItems: 'center', gap: 10 },
  back: { width: 24, height: 32, alignItems: 'center', justifyContent: 'center', marginLeft: -4 },
  backGlyph: { color: agentUpTheme.colors.textSecondary, fontSize: 26, lineHeight: 28 },
  kind: { ...auBox('diffKind'), ...auText('diffKind'), fontFamily: MONO, textAlign: 'center' },
  names: { flex: 1, minWidth: 0, flexDirection: 'row', alignItems: 'baseline', gap: 8 },
  name: { ...auText('diffName'), flexShrink: 0, maxWidth: '70%' },
  folder: { ...auText('diffFolder'), flex: 1, minWidth: 0 },
  countAdded: { ...auText('diffCountAdded'), fontFamily: MONO },
  countDeleted: { ...auText('diffCountDeleted'), fontFamily: MONO },
  loading: { marginVertical: 32 },
  note: { ...auBox('diffNote'), ...auText('diffNote'), lineHeight: 19 },
  body: { ...auBox('diffBody'), flex: 1 },
  bodyContent: { paddingBottom: 40 },
  block: { flexDirection: 'row' },
  number: {
    ...auBox('diffNumber'),
    ...auText('diffNumber'),
    height: DIFF_LINE_HEIGHT,
    lineHeight: DIFF_LINE_HEIGHT,
    paddingRight: 10,
    textAlign: 'right',
    fontFamily: MONO,
    borderLeftWidth: 2,
    borderLeftColor: 'transparent',
  },
  numberAdded: { ...auBox('diffNumberAdded'), ...auText('diffNumberAdded') },
  numberDeleted: { ...auBox('diffNumberDeleted'), ...auText('diffNumberDeleted') },
  codeScroll: { flex: 1 },
  codeColumn: { flexGrow: 1, minWidth: '100%' },
  line: { ...auBox('diffLine'), height: DIFF_LINE_HEIGHT, justifyContent: 'center', alignSelf: 'stretch' },
  lineAdded: auBox('diffLineAdded'),
  lineDeleted: auBox('diffLineDeleted'),
  code: { ...auText('diffCode'), paddingHorizontal: 12, lineHeight: DIFF_LINE_HEIGHT, fontFamily: MONO },
  wordAdded: auBox('diffWordAdded'),
  wordDeleted: auBox('diffWordDeleted'),
  fold: {
    ...auBox('diffFold'),
    height: DIFF_FOLD_HEIGHT,
    paddingLeft: 0,
    paddingRight: 12,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  foldRule: { ...auText('diffFoldRule'), width: 44, textAlign: 'center' },
  foldLabel: { ...auText('diffFoldLabel'), flex: 1, fontFamily: MONO },
  expand: { ...auBox('diffExpand'), alignItems: 'center', justifyContent: 'center' },
  expandText: auText('diffExpand'),
});
