import { Redirect } from 'expo-router';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { WorkspaceEmptyScreen } from '@/features/workspaces/components/WorkspaceEmptyScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { agentUpTheme } from '@agent-up/design-system/native';

export default function WorkspaceIndexRoute() {
  const { loading, selectedWorkspace } = useWorkspaces();

  if (loading && !selectedWorkspace) {
    return <View style={styles.loading}><ActivityIndicator color={agentUpTheme.colors.accent} /></View>;
  }

  if (!selectedWorkspace) return <WorkspaceEmptyScreen />;

  return <Redirect href={`/(main)/workspace/${selectedWorkspace.id}`} />;
}

const styles = StyleSheet.create({
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: agentUpTheme.colors.canvas },
});
