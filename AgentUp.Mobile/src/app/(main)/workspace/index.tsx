import { Redirect } from 'expo-router';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { WorkspaceEmptyScreen } from '@/features/workspaces/components/WorkspaceEmptyScreen';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';

export default function WorkspaceIndexRoute() {
  const { loading, selectedWorkspace } = useWorkspaces();

  if (loading && !selectedWorkspace) {
    return <View style={styles.loading}><ActivityIndicator color="#00d66b" /></View>;
  }

  if (!selectedWorkspace) return <WorkspaceEmptyScreen />;

  return <Redirect href={`/(main)/workspace/${selectedWorkspace.id}`} />;
}

const styles = StyleSheet.create({
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#000000' },
});
