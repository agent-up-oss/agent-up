import { ActivityIndicator, StyleSheet, View } from 'react-native';

// Shown by a dynamic route while the first workspace refresh is still in flight. Without it a cold
// deep link renders against an empty list and redirects away before its workspace can arrive.
export function ShellLoading() {
  return (
    <View accessibilityRole="progressbar" style={styles.container}>
      <ActivityIndicator color="#00d66b" />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { alignItems: 'center', backgroundColor: '#000000', flex: 1, justifyContent: 'center' },
});
