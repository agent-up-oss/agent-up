import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useAppShell } from '../controllers/AppShellContext';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

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
    ...auBox('mobileBar'),
    flexDirection: 'row',
    alignItems: 'center',
    gap: agentUpTheme.spacing[3],
  },
  stackButton: {
    ...auBox('titleTool'),
    alignItems: 'center',
    justifyContent: 'center',
  },
  stackIcon: { ...auText('chromeIcon'), fontSize: 10, lineHeight: 8 },
  title: {
    ...auText('heading'),
    flex: 1,
  },
  rightButton: {
    ...auBox('button', 'buttonSecondary', 'buttonCompact'),
    alignItems: 'center',
    justifyContent: 'center',
  },
  rightButtonText: { ...auText('buttonSecondary', 'buttonCompact') },
  rightSpacer: { width: agentUpTheme.controls.heightTouch },
});
