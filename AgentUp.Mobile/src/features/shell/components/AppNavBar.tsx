import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useAppShell } from '../controllers/AppShellContext';

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
    borderBottomColor: '#287038',
    backgroundColor: '#000000',
  },
  stackButton: {
    width: 42,
    height: 42,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#287038',
    backgroundColor: '#050505',
  },
  stackIcon: { color: '#2bf27a', fontSize: 10, lineHeight: 8 },
  title: {
    flex: 1,
    color: '#f5fbf7',
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
    borderColor: '#287038',
    backgroundColor: '#050505',
  },
  rightButtonText: { color: '#2bf27a', fontWeight: '700', fontSize: 13 },
  rightSpacer: { width: 42 },
});
