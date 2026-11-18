import { Linking, ScrollView, StyleSheet, Text, View } from 'react-native';
import { auBox, auText } from '@agent-up/design-system/native';
import { monoFont } from './monoFont';
import { parseMarkdown, type MarkdownBlock, type MarkdownInline } from '../providers/MarkdownProvider';

/**
 * An agent's markdown drawn with the design system's transcript styles. Nested Text cannot take
 * padding in React Native, so inline code carries the catalog's text style and surface colour.
 * A link opens only when the person taps it.
 */
export function MarkdownView({ markdown }: { markdown: string }) {
  return <View style={styles.stack}>{parseMarkdown(markdown).map((block, index) => <Block key={index} block={block} />)}</View>;
}

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
          <Text style={styles.codeText} selectable>{block.code}</Text>
        </ScrollView>
      </View>;
    case 'rule':
      return <View style={styles.rule} />;
    default:
      return <Text style={styles.paragraph} selectable><Inlines inlines={block.inlines} /></Text>;
  }
}

function Inlines({ inlines }: { inlines: MarkdownInline[] }) {
  return <>{inlines.map((inline, index) => {
    switch (inline.kind) {
      case 'bold': return <Text key={index} style={styles.bold}>{inline.text}</Text>;
      case 'italic': return <Text key={index} style={styles.italic}>{inline.text}</Text>;
      case 'code': return <Text key={index} style={styles.inlineCode}>{` ${inline.text} `}</Text>;
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
  rule: { height: StyleSheet.hairlineWidth, backgroundColor: auBox('mdQuote').borderLeftColor ?? undefined, marginVertical: 4 },
});
