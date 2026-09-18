import { useCallback, useEffect, useMemo, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { WorkspaceBranchPicker } from './WorkspaceBranchPicker';
import { GitChangesPanel } from './GitChangesPanel';
import { WorkspaceTabBar } from '@/features/shell/components/WorkspaceTabBar';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { getLog } from '../providers/GitApiProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export function GitOverviewScreen({ workspaceId }: { workspaceId: string }) {
  const router = useRouter();
  const { expireActiveCredential } = useServers();
  const { server } = useWorkspaces();
  const [commitCount, setCommitCount] = useState(0);

  const shellConfig = useMemo(() => ({
    title: 'Git',
    rightAction: null,
    sidebarContent: null,
  }), []);
  useShellConfig(shellConfig);

  const loadCount = useCallback(async () => {
    if (!server) return;
    try {
      const history = await getLog(server, workspaceId);
      setCommitCount(history?.commits.length ?? 0);
    } catch (cause) {
      if (isUnauthorized(cause)) expireActiveCredential();
    }
  }, [server, workspaceId, expireActiveCredential]);

  useEffect(() => { void loadCount(); }, [loadCount]);
  useEffect(() => {
    const timer = setInterval(() => { void loadCount(); }, 2500);
    return () => clearInterval(timer);
  }, [loadCount]);

  return (
    <View style={styles.screen}>
      <View style={styles.body}>
        <WorkspaceBranchPicker workspaceId={workspaceId} />
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Changes</Text>
          <GitChangesPanel
            workspaceId={workspaceId}
            mode="overview"
            onReview={() => router.push(`/(main)/workspace/${workspaceId}/git/review`)}
          />
        </View>
        <View style={styles.history}>
          <Text style={styles.sectionTitle}>History</Text>
          <View style={styles.historyRow}>
            <Text style={styles.count}>{commitCount} commit{commitCount === 1 ? '' : 's'}</Text>
            <Pressable
              testID="open-git-history"
              accessibilityRole="button"
              accessibilityLabel="View history"
              onPress={() => router.push(`/(main)/workspace/${workspaceId}/git/history`)}
              style={styles.historyButton}>
              <Text style={styles.historyButtonText}>View history</Text>
            </Pressable>
          </View>
        </View>
      </View>
      <WorkspaceTabBar workspaceId={workspaceId} active="git" />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  body: { flex: 1, padding: agentUpTheme.spacing[4], gap: 16, maxWidth: 672, width: '100%', alignSelf: 'center' },
  section: { flex: 1, gap: 8, minHeight: 0 },
  sectionTitle: auText('fieldLabel'),
  history: { ...auBox('card'), gap: 8 },
  historyRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  count: auText('muted'),
  historyButton: { ...auBox('button', 'buttonSecondary', 'buttonCompact'), alignItems: 'center', justifyContent: 'center' },
  historyButtonText: auText('buttonSecondary', 'buttonCompact'),
});
