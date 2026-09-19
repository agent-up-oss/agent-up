import { View, StyleSheet } from 'react-native';
import { WorkspaceRouteGate } from '@/features/workspaces/components/WorkspaceRouteGate';
import { GitHistoryPanel } from '@/features/git/components/GitHistoryPanel';
import { useInnerShell } from '@/features/shell/hooks/useInnerShell';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { agentUpTheme } from '@agent-up/design-system/native';

export default function WorkspaceGitHistoryRoute() {
  return (
    <WorkspaceRouteGate>
      {workspace => <GitHistoryScreen workspaceId={workspace.id} />}
    </WorkspaceRouteGate>
  );
}

function GitHistoryScreen({ workspaceId }: { workspaceId: string }) {
  useShellConfig(useInnerShell('History', `/(main)/workspace/${workspaceId}/git`));
  return (
    <View style={styles.screen}>
      <GitHistoryPanel workspaceId={workspaceId} />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, padding: agentUpTheme.spacing[4], backgroundColor: agentUpTheme.colors.canvas },
});
