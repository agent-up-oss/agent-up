import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { WorkspacePage } from '@/features/shell/components/WorkspacePage';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { workspaceScopeKey } from '@/features/workspaces/providers/WorkspaceScopeKeyProvider';
import { getDirectory } from '../providers/WorkspaceFilesApiProvider';
import {
  emptyFileTree,
  foldersToReload,
  needsListing,
  visibleFileRows,
  withExpanded,
  withListing,
  type FileTreeRow,
  type FileTreeState,
} from '../providers/FileTreeProvider';
import { FileRow } from './FileRow';

/**
 * The Files tab: every file in the workspace's clone, folders folded until opened. The top level
 * is read when the tab opens and each folder when it is first unfolded, so nothing is fetched
 * ahead of the reader. A changed file keeps its Git glyph and a folder counts the changes under it.
 */
export function FilesScreen({ workspaceId }: { workspaceId: string }) {
  const host = useWorkspaceHost(workspaceId);
  const { activeServer, expireActiveCredential } = useServers();
  const { server, workspaces } = useWorkspaces();
  const workspace = workspaces.find(item => item.id === workspaceId) ?? null;
  const scope = workspaceScopeKey(activeServer?.id ?? '', workspaceId);
  const [tree, setTree] = useState<FileTreeState>(emptyFileTree);
  const [loading, setLoading] = useState(false);
  const [opening, setOpening] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const generation = useRef(0);
  const treeRef = useRef(tree);
  treeRef.current = tree;

  useShellConfig(useMemo(() => ({ title: 'Files', rightAction: null, sidebarContent: null }), []));

  const fail = useCallback((cause: unknown) => {
    if (isUnauthorized(cause)) expireActiveCredential();
    setError(cause instanceof Error ? cause.message : String(cause));
  }, [expireActiveCredential]);

  const reload = useCallback(async (from: FileTreeState) => {
    const ticket = ++generation.current;
    if (!server) return;
    setLoading(true);
    setError(null);
    try {
      let next: FileTreeState = { folders: {}, expanded: from.expanded };
      for (const path of foldersToReload(from)) {
        const listing = await getDirectory(server, workspaceId, path);
        if (ticket !== generation.current) return;
        if (listing) next = withListing(next, listing);
        else if (path === '') {
          setError('This workspace is no longer on the Server.');
          next = emptyFileTree;
          break;
        }
      }
      setTree({ ...next, expanded: next.expanded.filter(path => next.folders[path] !== undefined) });
    } catch (cause) {
      if (ticket === generation.current) fail(cause);
    } finally {
      if (ticket === generation.current) setLoading(false);
    }
  }, [server, workspaceId, fail]);

  useEffect(() => {
    setTree(emptyFileTree);
    void reload(emptyFileTree);
  }, [scope, reload]);

  const toggle = useCallback(async (path: string) => {
    const current = treeRef.current;
    if (current.expanded.includes(path)) {
      setTree(withExpanded(current, path, false));
      return;
    }
    if (!needsListing(current, path)) {
      setTree(withExpanded(current, path, true));
      return;
    }
    if (!server) return;
    const ticket = generation.current;
    setOpening(path);
    try {
      const listing = await getDirectory(server, workspaceId, path);
      if (ticket !== generation.current) return;
      setTree(state => withExpanded(listing ? withListing(state, listing) : state, path, listing !== null));
    } catch (cause) {
      fail(cause);
    } finally {
      setOpening(null);
    }
  }, [server, workspaceId, fail]);

  const rows = useMemo(() => visibleFileRows(tree), [tree]);
  const root = workspace ? `${workspace.displayName} · ${workspace.branch || 'not on a git branch'}` : '';

  const renderRow = ({ item }: { item: FileTreeRow }) => item.kind === 'notice'
    ? <Text style={[auText('filesNotice'), { paddingLeft: 12 + item.depth * 20 }]}>{item.text}</Text>
    : (
      <FileRow
        entry={item.entry}
        depth={item.depth}
        expanded={item.expanded}
        loading={opening === item.entry.path}
        onToggle={() => void toggle(item.entry.path)}
        onOpen={() => host.open({ kind: 'file', path: item.entry.path })}
      />
    );

  return (
    <WorkspacePage workspaceId={workspaceId} active="files">
      <View style={styles.body}>
        <View style={[styles.toolbar, auBox('filesToolbar')]}>
          <Text style={auText('fieldLabel')}>Files</Text>
          <Text numberOfLines={1} style={[styles.root, auText('filesRoot')]}>{root}</Text>
          <Pressable
            testID="files-reload"
            accessibilityRole="button"
            accessibilityLabel="Reload files"
            onPress={() => void reload(treeRef.current)}
            style={[styles.reload, auBox('browserButton', 'gitQuiet')]}>
            <Text style={auText('browserButton')}>↻</Text>
          </Pressable>
        </View>
        {error ? <Text accessibilityRole="alert" style={styles.error}>{error}</Text> : null}
        <View style={[styles.list, auBox('gitChangeList')]}>
          {loading && rows.length === 0
            ? <ActivityIndicator style={styles.spinner} color={agentUpTheme.colors.accentSoft} />
            : (
              // Every unfolded row stays in the document. The Server lists directories first, so a
              // top-level file sits after every AgentUp.* folder; a windowed list would leave it
              // unmounted on a phone viewport until the person scrolled, and a Visible wait cannot.
              <ScrollView testID="files-tree" style={styles.tree}>
                {rows.length === 0 && !error
                  ? <Text style={auText('filesNotice')}>This workspace folder is empty.</Text>
                  : rows.map(row => (
                    <View key={row.kind === 'notice' ? row.key : row.entry.path}>
                      {renderRow({ item: row })}
                    </View>
                  ))}
              </ScrollView>
            )}
        </View>
      </View>
    </WorkspacePage>
  );
}

const styles = StyleSheet.create({
  body: { flex: 1, padding: agentUpTheme.spacing[4], gap: 12, width: '100%' },
  toolbar: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  root: { flex: 1, minWidth: 0 },
  reload: { alignItems: 'center', justifyContent: 'center' },
  error: { ...auText('fieldLabel'), color: agentUpTheme.colors.textDanger, textTransform: 'none' },
  list: { flex: 1, minHeight: 0 },
  tree: { flex: 1 },
  spinner: { marginTop: 24 },
});
