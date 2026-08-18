import { StatusBar } from 'expo-status-bar';
import { SafeAreaView, StyleSheet, Text, View } from 'react-native';

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
    backgroundColor: '#111827',
  },
  card: {
    width: '100%',
    maxWidth: 560,
    gap: 12,
    padding: 32,
    borderRadius: 24,
    backgroundColor: '#1f2937',
  },
  title: {
    color: '#f9fafb',
    fontSize: 36,
    fontWeight: '700',
  },
  subtitle: {
    color: '#d1d5db',
    fontSize: 20,
    lineHeight: 28,
  },
  detail: {
    color: '#9ca3af',
    fontSize: 15,
    lineHeight: 22,
  },
});
