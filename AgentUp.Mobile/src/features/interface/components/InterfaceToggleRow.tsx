import { Pressable, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox } from '@agent-up/design-system/native';
import { useInterfaceChoice } from '../controllers/InterfaceContext';

/**
 * The switch between the two interfaces, shown in whichever Settings screen is on screen.
 *
 * Both sides carry it: the classic Settings turns the command center on, the command center's
 * Settings turns it off. Neither client can strand someone in a shell they cannot leave.
 */
export function InterfaceToggleRow() {
  const { choice, persists, choose } = useInterfaceChoice();
  const on = choice === 'command-center';

  return (
    <View style={styles.row}>
      <View style={styles.body}>
        <Text style={styles.name}>Command center</Text>
        <Text style={styles.note}>
          {on
            ? 'You are using the new interface. Switching back restores the classic workspace shell. Your agents, workspaces and accounts are untouched either way.'
            : 'Try the new interface: one page per agent, with triage across the whole team. You can switch back from its Settings at any time.'}
        </Text>
        {persists ? null : (
          <Text style={styles.warning}>This device cannot store the choice, so it resets when the app reloads.</Text>
        )}
      </View>
      <Pressable
        accessibilityRole="switch"
        accessibilityState={{ checked: on }}
        accessibilityLabel="Command center interface"
        onPress={() => choose(on ? 'classic' : 'command-center')}
        style={[styles.track, on && styles.trackOn]}
      >
        <View style={[styles.knob, on && styles.knobOn]} />
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    ...auBox('card'),
    alignItems: 'center',
    gap: agentUpTheme.spacing[4],
  },
  body: { flex: 1, gap: 4 },
  name: { color: agentUpTheme.colors.textPrimary, fontSize: 15, fontWeight: '600' },
  note: { color: agentUpTheme.colors.textMuted, fontSize: 13, lineHeight: 19 },
  warning: { color: agentUpTheme.colors.textWarning, fontSize: 12, marginTop: 4 },
  track: {
    width: 46,
    height: 26,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderStrong,
    backgroundColor: agentUpTheme.colors.surfaceInput,
    padding: 2,
    justifyContent: 'center',
  },
  trackOn: { backgroundColor: agentUpTheme.colors.accentDark, borderColor: agentUpTheme.colors.accent },
  knob: { width: 18, height: 18, borderRadius: 999, backgroundColor: agentUpTheme.colors.textFaint },
  knobOn: { backgroundColor: agentUpTheme.colors.textPrimary, transform: [{ translateX: 20 }] },
});
