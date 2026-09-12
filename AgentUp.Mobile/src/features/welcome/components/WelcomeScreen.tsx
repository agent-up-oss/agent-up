import { StatusBar } from 'expo-status-bar';
import { StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

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
    padding: agentUpTheme.spacing[6],
    backgroundColor: agentUpTheme.colors.canvas,
  },
  card: {
    ...auBox('card'),
    width: '100%',
    maxWidth: 560,
    gap: agentUpTheme.spacing[3],
  },
  title: auText('title'),
  subtitle: auText('heading'),
  detail: auText('muted'),
});
