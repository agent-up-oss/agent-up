import { memo, useMemo } from 'react';
import { Linking, ScrollView, StyleSheet, Text, View } from 'react-native';
import { auBox, auText } from '@agent-up/design-system/native';
import { monoFont } from './monoFont';
import { blockKey, parseMarkdown, type MarkdownBlock, type MarkdownInline } from '../providers/MarkdownProvider';
import { highlightLines, languageForFence } from '../providers/SyntaxProvider';
import { SyntaxLine } from './SyntaxLine';

/**
 * An agent's markdown drawn with the design system's transcript styles. Nested Text cannot take
 * padding in React Native, so inline code carries the catalog's text style and surface colour.
 * A link opens only when the person taps it. A streaming reply re-renders only the blocks a
 * chunk changed: each block is memoised on its key, so the ones above the cursor stay put.
 */
export function MarkdownView({ markdown }: { markdown: string }) {
  const blocks = useMemo(() => parseMarkdown(markdown).map(block => ({ block, key: blockKey(block) })), [markdown]);
  return <View style={styles.stack}>{blocks.map((entry, index) => <MemoBlock key={index} blockKey={entry.key} block={entry.block} />)}</View>;
}

const MemoBlock = memo(function MemoBlock({ block }: { block: MarkdownBlock; blockKey: string }) {
  return <Block block={block} />;
}, (previous, next) => previous.blockKey === next.blockKey);

function Block({ block }: { block: MarkdownBlock }) {
  switch (block.kind) {
    case 'heading':
      return <Text style={[styles.heading, block.level > 2 && styles.headingMinor]}><Inlines inlines={block.inlines} /></Text>;
    case 'listItem':
      return <View style={[styles.listItem, { marginLeft: block.depth * 16 }]}>
        <Text style={styles.marker}>{block.marker}</Text>
        <Text style={styles.paragraphFlex}><Inlines inlines={block.inlines} /></Text>
      </View>;
    case 'quote':
      return <View style={styles.quote}><Text style={styles.paragraph}><Inlines inlines={block.inlines} /></Text></View>;
    case 'code':
      return <View style={styles.codeStack}>
        {block.language ? <Text style={styles.codeLabel}>{block.language}</Text> : null}
        <ScrollView horizontal style={styles.codeBlock} contentContainerStyle={styles.codeContent}>
          <Code code={block.code} language={block.language} />
        </ScrollView>
      </View>;
    case 'table':
      // React Native has no table layout, so the table is drawn a column at a time: every cell of
      // a column shares that column's width, and one-line cells keep each row the same height.
      return <ScrollView horizontal style={styles.table} testID="agent-md-table">
        <View style={styles.tableColumns}>{block.rows[0]!.map((_, column) => <View key={column}>
          {block.rows.map((row, rowIndex) => <View key={rowIndex} style={rowIndex === 0 ? styles.tableHead : styles.tableBody}>
            <Text numberOfLines={1} style={[rowIndex === 0 ? styles.tableHeaderCell : styles.tableCell, { textAlign: block.alignments[column] ?? 'left' }]}>
              <Inlines inlines={row[column] ?? []} />
            </Text>
          </View>)}
        </View>)}</View>
      </ScrollView>;
    case 'rule':
      return <View style={styles.rule} />;
    default:
      return <Text style={styles.paragraph} selectable><Inlines inlines={block.inlines} /></Text>;
  }
}

function Code({ code, language }: { code: string; language?: string }) {
  const lines = highlightLines(code, languageForFence(language));
  if (!lines) return <Text style={styles.codeText} selectable>{code}</Text>;
  return <View>{lines.map((tokens, index) => <SyntaxLine key={index} tokens={tokens} textClass="mdCodeText" />)}</View>;
}

function Inlines({ inlines }: { inlines: MarkdownInline[] }) {
  return <>{inlines.map((inline, index) => {
    switch (inline.kind) {
      case 'bold': return <Text key={index} style={styles.bold}>{inline.text}</Text>;
      case 'italic': return <Text key={index} style={styles.italic}>{inline.text}</Text>;
      case 'code': return <Text key={index} style={styles.inlineCode}>{`\u202f${inline.text}\u202f`}</Text>;
      case 'link': return <Text key={index} style={styles.link} accessibilityRole="link" onPress={() => { if (inline.url) void Linking.openURL(inline.url); }}>{inline.text}</Text>;
      default: return <Text key={index}>{inline.text}</Text>;
    }
  })}</>;
}

const styles = StyleSheet.create({
  stack: { gap: 8 },
  paragraph: { ...auText('mdParagraph'), lineHeight: 22 },
  paragraphFlex: { ...auText('mdParagraph'), lineHeight: 22, flex: 1 },
  heading: auText('mdHeading'),
  headingMinor: auText('mdHeadingMinor'),
  listItem: { ...auBox('mdListItem'), flexDirection: 'row', gap: 8 },
  marker: { ...auText('mdMarker'), lineHeight: 22, minWidth: 14 },
  quote: auBox('mdQuote'),
  codeStack: { gap: 4 },
  codeLabel: { ...auText('mdCodeLabel'), fontFamily: monoFont },
  codeBlock: auBox('mdCodeBlock'),
  codeContent: { flexGrow: 1 },
  codeText: { ...auText('mdCodeText'), fontFamily: monoFont },
  bold: { fontWeight: '600' },
  italic: { fontStyle: 'italic' },
  inlineCode: { ...auText('mdInlineCode'), fontFamily: monoFont, backgroundColor: auBox('mdInlineCode').backgroundColor },
  link: { ...auText('mdLink'), textDecorationLine: 'underline' },
  table: { ...auBox('mdTable'), flexGrow: 0, alignSelf: 'flex-start' },
  tableColumns: { flexDirection: 'row' },
  tableHead: auBox('mdTableHead'),
  tableBody: auBox('mdTableRow'),
  tableHeaderCell: { ...auText('mdTableHeaderCell'), ...cellBox('mdTableHeaderCell') },
  tableCell: { ...auText('mdTableCell'), ...cellBox('mdTableCell') },
  rule: { height: StyleSheet.hairlineWidth, backgroundColor: auBox('mdQuote').borderLeftColor ?? undefined, marginVertical: 4 },
});

/** A cell's padding from its catalog box; Text takes padding, not borders or backgrounds. */
function cellBox(name: 'mdTableHeaderCell' | 'mdTableCell') {
  const box = auBox(name) as Record<string, unknown>;
  return Object.fromEntries(Object.entries(box).filter(([key]) => key.startsWith('padding')));
}
