import { ScrollView, StyleSheet } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { GitChangesPanel } from './GitChangesPanel';
import { agentUpTheme } from '@agent-up/design-system/native';

/** @deprecated Use GitChangesPanel inside the agent Changes tab. */
export function GitChangesScreen() {
  return (
    <SafeAreaView style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <GitChangesPanel />
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: 20, paddingBottom: 32 },
});
