import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { auBox, auText } from '@agent-up/design-system/native';
import type { TranscriptItem } from '../models/AgentSession';
import { monoFont } from './monoFont';
import { presentToolCall } from '../providers/ToolCallProvider';

/**
 * One quiet row per tool call: status glyph, readable name, source, and the argument that
 * matters. Tapping it opens the input and a formatted output that stays bounded.
 */
export function ToolCallRow({ item }: { item: TranscriptItem }) {
  const [open, setOpen] = useState(false);
  const [all, setAll] = useState(false);
  const view = presentToolCall(item.title, item.toolKind, item.rawInput, item.output, item.status);
  const glyph = view.state === 'done' ? '✓' : view.state === 'failed' ? '✕' : '◐';
  const failed = view.state === 'failed';
  return <View style={styles.wrap}>
    <Pressable
      testID={`agent-tool-${item.toolCallId ?? item.id}`}
      accessibilityRole="button"
      accessibilityLabel={[view.title, view.source, view.summary, view.state].filter(Boolean).join(', ')}
      onPress={() => setOpen(current => !current)}
      style={styles.row}>
      <Text style={[styles.glyph, view.state === 'running' && styles.glyphRunning, failed && styles.glyphFailed]}>{glyph}</Text>
      <Text style={styles.title}>{view.title}</Text>
      {view.source ? <View style={styles.source}><Text style={styles.sourceText}>{view.source}</Text></View> : null}
      {view.summary ? <Text style={styles.summary} numberOfLines={1} ellipsizeMode="middle">{view.summary}</Text> : null}
    </Pressable>
    {open ? <View style={styles.detail}>
      {view.input ? <>
        <Text style={styles.label}>Input</Text>
        <View style={styles.output}><Text style={styles.outputText} selectable>{view.input}</Text></View>
      </> : null}
      {view.output ? <>
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
  detail: { ...auBox('toolCallDetail'), gap: 6 },
  label: auText('toolCallLabel'),
  output: { ...auBox('toolCallOutput'), maxHeight: 360 },
  outputFailed: auBox('toolCallOutputFailed'),
  outputText: { ...auText('toolCallOutputText'), fontFamily: monoFont },
  outputTextFailed: auText('toolCallOutputTextFailed'),
  more: auText('toolCallMore'),
});
