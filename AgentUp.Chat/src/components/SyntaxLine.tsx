import { Text, type TextStyle } from 'react-native';
import { auText } from '@agent-up/design-system/native';
import type { SyntaxToken } from '../providers/SyntaxProvider';
import { monoFont } from './monoFont';

/** One highlighted line: a text run per token, with the design system's syntax colour for its kind. */
export function SyntaxLine({ tokens, textClass, style }: { tokens: SyntaxToken[]; textClass: 'mdCodeText' | 'fileViewerCode'; style?: TextStyle }) {
  return <Text style={[auText(textClass), { fontFamily: monoFont }, style]} selectable>
    {tokens.map((token, index) => <Text key={index} style={auText(syntaxStyle(token.kind))}>{token.text}</Text>)}
    {tokens.length === 0 ? ' ' : null}
  </Text>;
}

function syntaxStyle(kind: string) {
  return `syntax${kind[0]!.toUpperCase()}${kind.slice(1)}` as Parameters<typeof auText>[0];
}
