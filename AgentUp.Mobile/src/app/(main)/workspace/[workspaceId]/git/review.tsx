import { View, StyleSheet } from 'react-native';
import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { GitChangesPanel } from '@/features/git/components/GitChangesPanel';
import { useInnerShell } from '@/features/shell/hooks/useInnerShell';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { agentUpTheme } from '@agent-up/design-system/native';

export default function WorkspaceGitReviewRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <GitReviewScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}

function GitReviewScreen({ workspaceId }: { workspaceId: string }) {
  useShellConfig(useInnerShell('Changes', `/(main)/workspace/${workspaceId}/git`));
  return (
    <View style={styles.screen}>
      <GitChangesPanel workspaceId={workspaceId} mode="review" />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, padding: agentUpTheme.spacing[4], backgroundColor: agentUpTheme.colors.canvas },
});
