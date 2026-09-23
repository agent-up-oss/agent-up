import { useCallback, useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { WorkspaceBranchPicker } from './WorkspaceBranchPicker';
import { GitChangesPanel } from './GitChangesPanel';
import { WorkspaceTabBar } from '@/features/shell/components/WorkspaceTabBar';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { agentUpTheme, auText } from '@agent-up/design-system/native';

export function GitOverviewScreen({ workspaceId }: { workspaceId: string }) {
  const router = useRouter();
  const [reloadNonce, setReloadNonce] = useState(0);
  const reload = useCallback(() => setReloadNonce(current => current + 1), []);

  const shellConfig = useMemo(() => ({
    title: 'Git',
    rightAction: null,
    sidebarContent: null,
  }), []);
  useShellConfig(shellConfig);

  return (
    <View style={styles.screen}>
      <View style={styles.body}>
        <WorkspaceBranchPicker
          workspaceId={workspaceId}
          onHistory={() => router.push(`/(main)/workspace/${workspaceId}/git/history`)}
          onReload={reload}
          reloadNonce={reloadNonce}
        />
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Changes</Text>
          <GitChangesPanel workspaceId={workspaceId} mode="overview" reloadNonce={reloadNonce} onMutated={reload} />
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
});
