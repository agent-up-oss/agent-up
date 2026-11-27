import { useCallback, useMemo, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { WorkspaceBranchPicker } from './WorkspaceBranchPicker';
import { GitChangesPanel } from './GitChangesPanel';
import { GitHostingRow } from './GitHostingRow';
import { WorkspacePage } from '@/features/shell/components/WorkspacePage';
import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';

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
      <View style={styles.page}>
        <GitChangesPanel
          workspaceId={workspaceId}
          mode="overview"
          reloadNonce={reloadNonce}
          onMutated={reload}
          onSelectedFilesChange={setSelectedFiles}
          header={
            <>
              <WorkspaceBranchPicker
                workspaceId={workspaceId}
                onHistory={() => host.open({ kind: 'gitHistory' })}
                onReload={reload}
                reloadNonce={reloadNonce}
                selectedFiles={selectedFiles}
              />
              <GitHostingRow workspaceId={workspaceId} reloadNonce={reloadNonce} />
            </>
          }
        />
      </View>
    </WorkspacePage>
  );
}

// The page clips the change sheet, which rests on its bottom edge above the workspace tabs.
const styles = StyleSheet.create({
  page: { flex: 1, overflow: 'hidden' },
});
