import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useAppShell } from '../controllers/AppShellContext';
import { agentUpTheme } from '@agent-up/design-system/native';

export function AppNavBar() {
  const insets = useSafeAreaInsets();
  const { config, openSidebar } = useAppShell();
  const rightAction = config.rightAction;

  return (
    <View style={[styles.bar, { paddingTop: insets.top + 8 }]}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Open sidebar"
        onPress={openSidebar}
        style={styles.stackButton}>
        <Text style={styles.stackIcon}>▰</Text>
        <Text style={styles.stackIcon}>▰</Text>
        <Text style={styles.stackIcon}>▰</Text>
      </Pressable>
      <Text accessibilityRole="header" numberOfLines={1} style={styles.title}>{config.title}</Text>
      {rightAction
        ? <Pressable
            accessibilityRole="button"
            accessibilityLabel={rightAction.accessibilityLabel ?? rightAction.label}
            onPress={rightAction.onPress}
            style={styles.rightButton}>
            <Text style={styles.rightButtonText}>{rightAction.label}</Text>
          </Pressable>
        : <View style={styles.rightSpacer} />}
    </View>
  );
}

const styles = StyleSheet.create({
  bar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    paddingHorizontal: 14,
    paddingBottom: 10,
    borderBottomWidth: 1,
    borderBottomColor: agentUpTheme.colors.borderSelected,
    backgroundColor: agentUpTheme.colors.canvas,
  },
  stackButton: {
    width: 42,
    height: 42,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSelected,
    backgroundColor: agentUpTheme.colors.surface,
  },
  stackIcon: { color: agentUpTheme.colors.accentSoft, fontSize: 10, lineHeight: 8 },
  title: {
    flex: 1,
    color: agentUpTheme.colors.textPrimary,
    fontSize: 20,
    lineHeight: 24,
    fontWeight: '800',
  },
  rightButton: {
    minHeight: 36,
    paddingHorizontal: 12,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSelected,
    backgroundColor: agentUpTheme.colors.surface,
  },
  rightButtonText: { color: agentUpTheme.colors.accentSoft, fontWeight: '700', fontSize: 13 },
  rightSpacer: { width: 42 },
});
