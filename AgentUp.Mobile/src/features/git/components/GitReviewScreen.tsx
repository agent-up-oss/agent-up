import { StyleSheet, View } from 'react-native';
import { GitChangesPanel } from './GitChangesPanel';
import { GitHostingRow } from './GitHostingRow';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaceInnerShell } from '@/features/shell/hooks/useWorkspaceInnerShell';
import { agentUpTheme } from '@agent-up/design-system/native';

/** The changes on a workspace, read for review: its hosting above, the files below. */
export function GitReviewScreen({ workspaceId }: { workspaceId: string }) {
  useShellConfig(useWorkspaceInnerShell(workspaceId, 'Changes', GIT));
  return (
    <View style={styles.screen}>
      <GitHostingRow workspaceId={workspaceId} detail />
      <GitChangesPanel workspaceId={workspaceId} mode="review" />
    </View>
  );
}

const GIT = { kind: 'git' } as const;

const styles = StyleSheet.create({
  screen: { flex: 1, padding: agentUpTheme.spacing[4], backgroundColor: agentUpTheme.colors.canvas, gap: agentUpTheme.spacing[3] },
});
