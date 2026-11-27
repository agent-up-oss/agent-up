import { useCallback, useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { WorkspaceBranchPicker } from './WorkspaceBranchPicker';
import { GitChangesPanel } from './GitChangesPanel';
import { GitHostingRow } from './GitHostingRow';
import { WorkspacePage } from '@/features/shell/components/WorkspacePage';
import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { agentUpTheme, auText } from '@agent-up/design-system/native';

export function GitOverviewScreen({ workspaceId }: { workspaceId: string }) {
  const host = useWorkspaceHost(workspaceId);
  const [reloadNonce, setReloadNonce] = useState(0);
  const [selectedFiles, setSelectedFiles] = useState<string[]>([]);
  const reload = useCallback(() => setReloadNonce(current => current + 1), []);

  const shellConfig = useMemo(() => ({
    title: 'Git',
    rightAction: null,
    sidebarContent: null,
  }), []);
  useShellConfig(shellConfig);

  return (
    <WorkspacePage workspaceId={workspaceId} active="git">
      <View style={styles.body}>
        <WorkspaceBranchPicker
          workspaceId={workspaceId}
          onHistory={() => host.open({ kind: 'gitHistory' })}
          onReload={reload}
          reloadNonce={reloadNonce}
          selectedFiles={selectedFiles}
        />
        <GitHostingRow workspaceId={workspaceId} reloadNonce={reloadNonce} />
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Changes</Text>
          <GitChangesPanel
            workspaceId={workspaceId}
            mode="overview"
            reloadNonce={reloadNonce}
            onMutated={reload}
            onSelectedFilesChange={setSelectedFiles}
          />
        </View>
      </View>
    </WorkspacePage>
  );
}

const styles = StyleSheet.create({
  body: { flex: 1, padding: agentUpTheme.spacing[4], gap: 16, width: '100%' },
  section: { flex: 1, gap: 8, minHeight: 0 },
  sectionTitle: auText('fieldLabel'),
});
