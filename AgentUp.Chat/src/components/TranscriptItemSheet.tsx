import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { auBox, auText } from '@agent-up/design-system/native';
import type { TranscriptItem } from '../models/AgentSession';
import { visibleText } from '../providers/AgentEventPresentationProvider';
import { presentSheet, type SheetArgument } from '../providers/SheetProvider';
import { languageForPath } from '../providers/SyntaxProvider';
import { BottomSheet, sheetGutter } from './BottomSheet';
import { MarkdownView } from './MarkdownView';
import { monoFont } from './monoFont';
import { DiffRow, presentToolItem } from './ToolCallRow';

/** A diff longer than this is drawn in part until asked, so a generated file does not stall the sheet. */
const sheetDiffLines = 400;

/**
 * One transcript item opened to read: a thought as prose, an edit as its whole diff, any other
 * call as its arguments and output. The sheet follows the item, so a call still running fills in.
 */
export function TranscriptItemSheet({ item, live, onClose }: { item: TranscriptItem; live: boolean; onClose: () => void }) {
  const sheet = presentSheet(item, live);
  return <BottomSheet title={sheet.title} onClose={onClose}>
    {sheet.meta ? <Text testID="agent-sheet-meta" style={styles.meta} selectable>{sheet.meta}</Text> : null}
    {item.role === 'thought'
      ? <View testID="agent-sheet-thought"><MarkdownView markdown={visibleText(item.text)} /></View>
      : <ToolSheetBody item={item} arguments={sheet.arguments} />}
  </BottomSheet>;
}

function ToolSheetBody({ item, arguments: rows }: { item: TranscriptItem; arguments: SheetArgument[] }) {
  const [wholeDiff, setWholeDiff] = useState(false);
  const view = presentToolItem(item);
  const failed = view.state === 'failed';
  const diff = view.diff;
  const lines = diff ? (wholeDiff ? diff.lines : diff.lines.slice(0, sheetDiffLines)) : [];
  return <>
    {rows.map(row => <View key={row.label} style={styles.argument}>
      <Text style={styles.label}>{row.label}</Text>
      <Text style={styles.value} selectable>{row.value}</Text>
    </View>)}
    {diff ? <View style={styles.bleed}>
      <ScrollView horizontal contentContainerStyle={styles.diffContent} testID={`agent-tool-diff-${item.toolCallId ?? item.id}`}>
        <View style={styles.diffLines}>{lines.map((line, index) => <DiffRow key={index} line={line} language={languageForPath(diff.path)} />)}</View>
      </ScrollView>
      {!wholeDiff && diff.lines.length > sheetDiffLines
        ? <Pressable accessibilityRole="button" onPress={() => setWholeDiff(true)} style={styles.more}><Text style={styles.label}>{`Show all ${diff.lines.length} lines`}</Text></Pressable>
        : null}
    </View> : null}
    {!diff && view.fullOutput ? <View style={styles.section}>
      <Text style={styles.label}>Output</Text>
      <ScrollView horizontal style={[styles.code, failed && styles.codeFailed]} contentContainerStyle={styles.codeContent}>
        <Text testID="agent-sheet-output" style={[styles.codeText, failed && styles.codeTextFailed]} selectable>{view.fullOutput}</Text>
      </ScrollView>
    </View> : null}
    {!diff && !view.fullOutput ? <Text style={styles.meta}>{view.state === 'running' ? 'Waiting for output…' : 'No output.'}</Text> : null}
  </>;
}

const styles = StyleSheet.create({
  meta: { ...auText('sheetMeta'), lineHeight: 20 },
  section: { gap: 8 },
  argument: { gap: 2 },
  label: auText('sheetLabel'),
  value: { ...auText('sheetBody'), lineHeight: 24 },
  // A diff runs edge to edge: its gutter is the margin.
  bleed: { marginHorizontal: -sheetGutter, gap: 8 },
  diffContent: { minWidth: '100%' },
  diffLines: { flexGrow: 1 },
  more: { paddingHorizontal: sheetGutter },
  code: auBox('sheetCode'),
  codeFailed: auBox('sheetCodeFailed'),
  codeContent: { minWidth: '100%' },
  codeText: { ...auText('sheetCodeText'), fontFamily: monoFont, lineHeight: 18 },
  codeTextFailed: auText('sheetCodeTextFailed'),
});
