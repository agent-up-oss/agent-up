import { StatusBar } from 'expo-status-bar';
import { StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { agentUpTheme } from '@agent-up/design-system/native';

export function WelcomeScreen() {
  return (
    <SafeAreaView style={styles.screen}>
      <View style={styles.card}>
        <Text accessibilityRole="header" style={styles.title}>
          Agent-Up
        </Text>
        <Text style={styles.subtitle}>Your development workspaces, wherever you are.</Text>
        <Text style={styles.detail}>
          Expo client ready for Android, iOS, and the installable web app.
        </Text>
      </View>
      <StatusBar style="light" />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 24,
    backgroundColor: agentUpTheme.colors.canvas,
  },
  card: {
    width: '100%',
    maxWidth: 560,
    gap: 12,
    padding: 32,
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSelected,
    borderRadius: 8,
    backgroundColor: agentUpTheme.colors.surface,
  },
  title: {
    color: agentUpTheme.colors.textPrimary,
    fontSize: 36,
    fontWeight: '700',
  },
  subtitle: {
    color: agentUpTheme.colors.textPrimary,
    fontSize: 20,
    lineHeight: 28,
  },
  detail: {
    color: agentUpTheme.colors.textMuted,
    fontSize: 15,
    lineHeight: 22,
  },
});
