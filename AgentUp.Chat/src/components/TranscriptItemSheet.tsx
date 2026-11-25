import { useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import * as Clipboard from 'expo-clipboard';
import { auBox, auText } from '@agent-up/design-system/native';
import type { TranscriptItem } from '../models/AgentSession';
import { visibleText } from '../providers/AgentEventPresentationProvider';
import { presentSheet, type SheetArgument } from '../providers/SheetProvider';
import { languageForPath } from '../providers/SyntaxProvider';
import { BottomSheet } from './BottomSheet';
import { MarkdownView } from './MarkdownView';
import { monoFont } from './monoFont';
import { DiffRow, presentToolItem } from './ToolCallRow';

/** A diff longer than this is drawn in part until asked, so a generated file does not stall the sheet. */
const sheetDiffLines = 400;
const copiedMs = 1500;

/**
 * One transcript item opened to read. The header says what it did and how that went. The body
 * sits on the sheet with no borders of its own: a thought as prose, details as label and value
 * lines, and output or a diff as one soft block under a quiet heading with its action. The sheet
 * follows the item, so a call still running fills in.
 */
export function TranscriptItemSheet({ item, live, onClose }: { item: TranscriptItem; live: boolean; onClose: () => void }) {
  const sheet = presentSheet(item, live);
  return <BottomSheet title={sheet.title} status={sheet.status} onClose={onClose}>
    {item.role === 'thought'
      ? <View testID="agent-sheet-thought"><MarkdownView markdown={visibleText(item.text)} /></View>
      : <ToolSheetBody item={item} details={sheet.arguments} />}
  </BottomSheet>;
}

function ToolSheetBody({ item, details }: { item: TranscriptItem; details: SheetArgument[] }) {
  const [wholeDiff, setWholeDiff] = useState(false);
  const view = presentToolItem(item);
  const diff = view.diff;
  const lines = diff ? (wholeDiff ? diff.lines : diff.lines.slice(0, sheetDiffLines)) : [];
  return <>
    {details.length ? <View testID="agent-sheet-details" style={styles.facts}>
      {details.map(row => <View key={row.label} style={styles.fact}>
        <Text style={styles.factLabel} numberOfLines={1}>{row.label}</Text>
        <Text style={styles.factValue} selectable>{row.value}</Text>
      </View>)}
    </View> : null}
    {diff ? <View style={styles.section}>
      <View style={styles.sectionHeader}>
        <Text style={[styles.sectionTitle, styles.path]} numberOfLines={1} ellipsizeMode="head">{diff.path ?? 'Changes'}</Text>
        {diff.added > 0 ? <Text style={styles.added}>+{diff.added}</Text> : null}
        {diff.removed > 0 ? <Text style={styles.removed}>−{diff.removed}</Text> : null}
      </View>
      <View style={styles.block}>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.diffContent} testID={`agent-tool-diff-${item.toolCallId ?? item.id}`}>
          <View style={styles.diffLines}>{lines.map((line, index) => <DiffRow key={index} line={line} language={languageForPath(diff.path)} />)}</View>
        </ScrollView>
      </View>
      {!wholeDiff && diff.lines.length > sheetDiffLines
        ? <Pressable accessibilityRole="button" onPress={() => setWholeDiff(true)} style={styles.sectionHeader}><Text style={styles.sectionAction}>{`Show all ${diff.lines.length} lines`}</Text></Pressable>
        : null}
    </View> : null}
    {!diff ? <View style={styles.section}>
      <View style={styles.sectionHeader}>
        <Text style={styles.sectionTitle}>Output</Text>
        {view.fullOutput ? <CopyButton text={view.fullOutput} /> : null}
      </View>
      <View style={styles.block}>
        <View style={styles.code}>
          {view.fullOutput
            ? <Text testID="agent-sheet-output" style={styles.codeText} selectable>{view.fullOutput}</Text>
            : <Text style={styles.factLabel}>{view.state === 'running' ? 'Waiting for output…' : 'No output'}</Text>}
        </View>
      </View>
    </View> : null}
  </>;
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), copiedMs);
    return () => clearTimeout(timer);
  }, [copied]);
  return <Pressable
    testID="agent-sheet-copy"
    accessibilityRole="button"
    accessibilityLabel={copied ? 'Copied' : 'Copy output'}
    hitSlop={12}
    onPress={() => { void Clipboard.setStringAsync(text).then(() => setCopied(true)); }}>
    <Text style={styles.sectionAction}>{copied ? 'Copied' : 'Copy'}</Text>
  </Pressable>;
}

const styles = StyleSheet.create({
  facts: { ...auBox('sheetFacts'), gap: 8 },
  fact: { ...auBox('sheetFact'), flexDirection: 'row', alignItems: 'baseline', gap: 16 },
  factLabel: auText('sheetFactLabel'),
  factValue: { ...auText('sheetFactValue'), flex: 1, lineHeight: 20 },
  section: { ...auBox('sheetSection'), gap: 8 },
  sectionHeader: { ...auBox('sheetSectionHeader'), flexDirection: 'row', alignItems: 'center', gap: 8 },
  sectionTitle: { ...auText('sheetSectionTitle'), flex: 1 },
  sectionAction: auText('sheetSectionAction'),
  path: { fontFamily: monoFont },
  added: { ...auText('gitInsertions'), fontFamily: monoFont },
  removed: { ...auText('gitDeletions'), fontFamily: monoFont },
  block: auBox('sheetBlock'),
  diffContent: { minWidth: '100%' },
  diffLines: { flexGrow: 1 },
  code: auBox('sheetCode'),
  codeText: { ...auText('sheetCodeText'), fontFamily: monoFont, lineHeight: 18 },
});
