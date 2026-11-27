import { StyleSheet, View } from 'react-native';
import { GitHistoryPanel } from './GitHistoryPanel';
import { ReadingContent } from '@/features/shell/components/ReadingContent';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaceInnerShell } from '@/features/shell/hooks/useWorkspaceInnerShell';
import { agentUpTheme } from '@agent-up/design-system/native';

/** What has already landed on this workspace's branch. */
export function GitHistoryScreen({ workspaceId }: { workspaceId: string }) {
  useShellConfig(useWorkspaceInnerShell(workspaceId, 'History', GIT));
  return (
    <ReadingContent>
      <View style={styles.screen}>
        <GitHistoryPanel workspaceId={workspaceId} />
      </View>
    </ReadingContent>
  );
}

const GIT = { kind: 'git' } as const;

const styles = StyleSheet.create({
  screen: { flex: 1, padding: agentUpTheme.spacing[4], backgroundColor: agentUpTheme.colors.canvas },
});
