import { StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { GitChangesPanel } from './GitChangesPanel';
import { agentUpTheme } from '@agent-up/design-system/native';

/** @deprecated Use GitChangesPanel on the Git Review page. */
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
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { flex: 1, padding: 20, paddingBottom: 12 },
});
