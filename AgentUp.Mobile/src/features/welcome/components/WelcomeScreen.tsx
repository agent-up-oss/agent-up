import { StatusBar } from 'expo-status-bar';
import { StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export function WelcomeScreen() {
  return (
    <SafeAreaView style={styles.screen}>
      <View style={styles.card}>
        <Text style={styles.eyebrow}>Agent-Up</Text>
        <Text accessibilityRole="header" style={styles.title}>
          Your development workspaces
        </Text>
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
    ...auBox('signIn'),
    width: '100%',
    maxWidth: 416,
    gap: agentUpTheme.spacing[3],
  },
  eyebrow: auText('eyebrow'),
  title: auText('pageTitle'),
  detail: auText('muted'),
});
