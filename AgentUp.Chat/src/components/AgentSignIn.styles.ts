import { StyleSheet } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export const styles = StyleSheet.create({
  panel: { gap: 8 },
  instructions: auText('workspaceName'),
  primary: { ...auBox('button', 'buttonCompact'), alignItems: 'center' },
  primaryText: auText('button', 'buttonCompact'),
  secondary: { ...auBox('button', 'buttonSecondary', 'buttonCompact'), alignItems: 'center' },
  secondaryText: auText('buttonSecondary', 'buttonCompact'),
  url: { ...auText('muted'), color: agentUpTheme.colors.textInfo, textDecorationLine: 'underline' },
  code: {
    ...auText('mono'),
    color: agentUpTheme.colors.accentSoft,
    fontSize: agentUpTheme.typography.sizeUiXl,
    fontWeight: '600',
    letterSpacing: 1,
  },
  codeEntry: { gap: 8 },
  input: { ...auBox('input'), ...auText('input') },
  modal: { flex: 1, backgroundColor: agentUpTheme.colors.surface },
});
