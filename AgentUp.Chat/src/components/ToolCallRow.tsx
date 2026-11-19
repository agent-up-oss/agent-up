import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { auBox, auText } from '@agent-up/design-system/native';
import type { TranscriptItem } from '../models/AgentSession';
import { monoFont } from './monoFont';
import { SyntaxLine } from './SyntaxLine';
import { presentToolFacts, type ToolCallView } from '../providers/ToolCallProvider';
import { languageForPath } from '../providers/SyntaxProvider';
import { tokenizeLine } from '@agent-up/design-system/syntax';
import type { DiffLine } from '../providers/DiffProvider';
import type { WorkEntry } from '../providers/AgentEventPresentationProvider';

/** Diff lines shown before the rest is a tap away. */
export const diffPreviewLines = 40;

export function presentToolItem(item: TranscriptItem): ToolCallView {
  return presentToolFacts({ ...item, title: item.title, kind: item.toolKind });
}

/**
 * One quiet row per tool call: status glyph, readable name, source, the argument that matters,
 * what an edit changed and how long the call took. Tapping it opens an edit's diff, or the input
 * and a formatted output that stays bounded.
 */
export function ToolCallRow({ item }: { item: TranscriptItem }) {
  const [open, setOpen] = useState(false);
  const [all, setAll] = useState(false);
  const [wholeDiff, setWholeDiff] = useState(false);
  const view = presentToolItem(item);
  const glyph = view.state === 'done' ? '✓' : view.state === 'failed' ? '✕' : '◐';
  const failed = view.state === 'failed';
  const diff = view.diff;
  const diffLines = diff ? (wholeDiff ? diff.lines : diff.lines.slice(0, diffPreviewLines)) : [];
  return <View style={styles.wrap}>
    <Pressable
      testID={`agent-tool-${item.toolCallId ?? item.id}`}
      accessibilityRole="button"
      accessibilityLabel={[view.title, view.source, view.summary, diff ? `+${diff.added} −${diff.removed}` : undefined, view.duration, view.state].filter(Boolean).join(', ')}
      onPress={() => setOpen(current => !current)}
      style={styles.row}>
      <Text style={[styles.glyph, view.state === 'running' && styles.glyphRunning, failed && styles.glyphFailed]}>{glyph}</Text>
      <Text style={styles.title}>{view.title}</Text>
      {view.source ? <View style={styles.source}><Text style={styles.sourceText}>{view.source}</Text></View> : null}
      {view.summary ? <Text style={styles.summary} numberOfLines={1} ellipsizeMode="middle">{view.summary}</Text> : <View style={styles.spacer} />}
      {diff && diff.added > 0 ? <Text style={styles.added}>+{diff.added}</Text> : null}
      {diff && diff.removed > 0 ? <Text style={styles.removed}>−{diff.removed}</Text> : null}
      {view.duration ? <Text style={styles.duration}>{view.duration}</Text> : null}
    </Pressable>
    {open ? <View style={styles.detail}>
      {diff ? <>
        {diff.path ? <Text style={styles.label}>{diff.path}</Text> : null}
        <ScrollView horizontal style={styles.diff} contentContainerStyle={styles.diffContent} testID={`agent-tool-diff-${item.toolCallId ?? item.id}`}>
          <View style={styles.diffLines}>{diffLines.map((line, index) => <DiffRow key={index} line={line} language={languageForPath(diff.path)} />)}</View>
        </ScrollView>
        {!wholeDiff && diff.lines.length > diffPreviewLines
          ? <Pressable accessibilityRole="button" onPress={() => setWholeDiff(true)}><Text style={styles.more}>{`Show all ${diff.lines.length} lines`}</Text></Pressable>
          : null}
      </> : null}
      {!diff && view.input ? <>
        <Text style={styles.label}>Input</Text>
        <View style={styles.output}><Text style={styles.outputText} selectable>{view.input}</Text></View>
      </> : null}
      {!diff && view.output ? <>
        <Text style={styles.label}>Output</Text>
        <ScrollView style={[styles.output, failed && styles.outputFailed]} nestedScrollEnabled>
          <Text style={[styles.outputText, failed && styles.outputTextFailed]} selectable>{all ? view.fullOutput : view.output}</Text>
        </ScrollView>
        {!all && view.hiddenLines > 0
          ? <Pressable accessibilityRole="button" onPress={() => setAll(true)}><Text style={styles.more}>{view.hiddenLines === 1 ? 'Show 1 more line' : `Show ${view.hiddenLines} more lines`}</Text></Pressable>
          : null}
      </> : null}
    </View> : null}
  </View>;
}

/** Consecutive finished reads or searches as one row that opens to the calls. */
export function ToolGroupRow({ entry }: { entry: Extract<WorkEntry, { type: 'group' }> }) {
  const [open, setOpen] = useState(false);
  return <View style={styles.wrap}>
    <Pressable testID={`agent-tool-group-${entry.id}`} accessibilityRole="button" accessibilityLabel={`${entry.title}, ${entry.summary}`} onPress={() => setOpen(current => !current)} style={styles.row}>
      <Text style={styles.glyph}>✓</Text>
      <Text style={styles.title}>{entry.title}</Text>
      <Text style={styles.summary} numberOfLines={1} ellipsizeMode="tail">{entry.summary}</Text>
      <Text style={styles.duration}>{open ? '⌄' : '›'}</Text>
    </Pressable>
    {open ? <View style={[styles.detail, styles.group]}>{entry.items.map(item => <ToolCallRow key={item.id} item={item} />)}</View> : null}
  </View>;
}

function DiffRow({ line, language }: { line: DiffLine; language: string }) {
  const tokens = line.kind === 'hunk' ? [{ kind: 'plain' as const, text: line.text }] : tokenizeLine(line.text, language);
  const box = line.kind === 'added' ? auBox('fileViewerLine', 'fileViewerLineAdded') : line.kind === 'deleted' ? auBox('fileViewerLine', 'fileViewerLineDeleted') : line.kind === 'hunk' ? auBox('fileViewerLine', 'fileViewerLineHunk') : auBox('fileViewerLine');
  const prefix = line.kind === 'added' ? auText('fileViewerPrefix', 'fileViewerPrefixAdded') : line.kind === 'deleted' ? auText('fileViewerPrefix', 'fileViewerPrefixDeleted') : auText('fileViewerPrefix');
  return <View style={[box, styles.diffLine]}>
    <Text style={[auText('fileViewerGutter'), styles.gutter]}>{line.newNumber ?? line.oldNumber ?? ''}</Text>
    <Text style={[prefix, styles.prefix]}>{line.kind === 'added' ? '+' : line.kind === 'deleted' ? '-' : ' '}</Text>
    <SyntaxLine tokens={tokens} textClass="fileViewerCode" />
  </View>;
}

const styles = StyleSheet.create({
  wrap: { gap: 4 },
  row: { ...auBox('toolCall'), flexDirection: 'row', alignItems: 'center', gap: 8 },
  glyph: auText('toolCallGlyph'),
  glyphRunning: auText('toolCallGlyphRunning'),
  glyphFailed: auText('toolCallGlyphFailed'),
  title: auText('toolCallTitle'),
  source: auBox('toolCallSource'),
  sourceText: auText('toolCallSource'),
  summary: { ...auText('toolCallSummary'), fontFamily: monoFont, flex: 1 },
  spacer: { flex: 1 },
  added: { ...auText('gitInsertions'), fontFamily: monoFont },
  removed: { ...auText('gitDeletions'), fontFamily: monoFont },
  duration: auText('toolCallSummary'),
  detail: { ...auBox('toolCallDetail'), gap: 6 },
  group: { gap: 4 },
  label: auText('toolCallLabel'),
  output: { ...auBox('toolCallOutput'), maxHeight: 360 },
  outputFailed: auBox('toolCallOutputFailed'),
  outputText: { ...auText('toolCallOutputText'), fontFamily: monoFont },
  outputTextFailed: auText('toolCallOutputTextFailed'),
  diff: { ...auBox('toolCallOutput'), paddingHorizontal: 0, maxHeight: 420 },
  diffContent: { minWidth: '100%' },
  diffLines: { flexGrow: 1 },
  diffLine: { flexDirection: 'row' },
  gutter: { width: 36, textAlign: 'right', fontFamily: monoFont },
  prefix: { width: 16, textAlign: 'center', fontFamily: monoFont },
  more: auText('toolCallMore'),
});
