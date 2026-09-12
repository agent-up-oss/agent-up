import { StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { GitChangesPanel } from './GitChangesPanel';

/** @deprecated Use GitChangesPanel inside the agent Changes tab. */
export function GitChangesScreen() {
  return (
    <SafeAreaView style={styles.screen}>
      <View style={styles.content}>
        <GitChangesPanel />
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#000000' },
  content: { flex: 1, padding: 20, paddingBottom: 12 },
});
