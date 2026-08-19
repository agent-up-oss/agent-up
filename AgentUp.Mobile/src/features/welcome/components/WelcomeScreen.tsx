import { StatusBar } from 'expo-status-bar';
import { StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

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
    backgroundColor: '#000000',
  },
  card: {
    width: '100%',
    maxWidth: 560,
    gap: 12,
    padding: 32,
    borderWidth: 1,
    borderColor: '#287038',
    borderRadius: 8,
    backgroundColor: '#050505',
  },
  title: {
    color: '#f5fbf7',
    fontSize: 36,
    fontWeight: '700',
  },
  subtitle: {
    color: '#f5fbf7',
    fontSize: 20,
    lineHeight: 28,
  },
  detail: {
    color: '#aebcb3',
    fontSize: 15,
    lineHeight: 22,
  },
});
