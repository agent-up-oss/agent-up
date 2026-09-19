import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { GitChangeNode, GitChangeTree, GitFileDiff } from '../models/GitChanges';
import type { CommitQueue } from '../models/CommitQueue';
import type { GitConfirmCopy } from '../providers/GitBranchPickerProvider';
import { commitFiles, discardFiles, getChanges, getCommitQueue, getFileDiff } from '../providers/GitApiProvider';
import { createRequestGate, type RequestGate } from '../providers/RequestGateProvider';
import {
  canCommitSelection,
  canDiscardSelection,
  flattenChangeTree,
  gitCommitConfirmCopy,
  gitDiscardConfirmCopy,
  gitStaleTreeConfirmCopy,
  isChangeTreeStale,
  isDirectorySelected,
  retainCollapsedPaths,
  retainSelectedPaths,
  selectedChangeStatusCounts,
  selectedFilePaths,
  toggleDirectoryCollapsed,
  toggleNodeSelection,
  visibleChangeNodes,
} from '../providers/GitChangeTreeProvider';
import { GitChangeRow } from './GitChangeRow';
import { GitConfirmDialog } from './GitConfirmDialog';
import { GitFileViewer } from './GitFileViewer';

const POLL_MS = 2500;

type PendingConfirm = { copy: GitConfirmCopy; action: () => void };

export function GitChangesPanel({
  workspaceId: workspaceIdProp,
  mode = 'review',
  onOpenFile,
  reloadNonce = 0,
}: {
  workspaceId?: string;
  mode?: 'overview' | 'review';
  onOpenFile?: (path: string) => void;
  reloadNonce?: number;
} = {}) {
  const { expireActiveCredential } = useServers();
  const { server, selectedWorkspace } = useWorkspaces();
  const workspaceId = workspaceIdProp ?? selectedWorkspace?.id ?? null;
  const overview = mode === 'overview';

  const [tree, setTree] = useState<GitChangeTree | null>(null);
  const [queue, setQueue] = useState<CommitQueue | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [collapsed, setCollapsed] = useState<string[]>([]);
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [stale, setStale] = useState(false);
  const [confirm, setConfirm] = useState<PendingConfirm | null>(null);
  const [diff, setDiff] = useState<GitFileDiff | null>(null);
  const [diffPath, setDiffPath] = useState<string | null>(null);
  const [diffLoading, setDiffLoading] = useState(false);

  const nodes = useMemo(() => flattenChangeTree(tree), [tree]);
  const visible = useMemo(() => visibleChangeNodes(nodes, collapsed), [nodes, collapsed]);

  const gates = useRef<{ tree: RequestGate; diff: RequestGate; mutate: RequestGate } | null>(null);
  gates.current ??= { tree: createRequestGate(), diff: createRequestGate(), mutate: createRequestGate() };
  const { tree: treeGate, diff: diffGate, mutate: mutateGate } = gates.current;
  const inflightLoads = useRef(0);
  const mutating = useRef(false);
  const treeRef = useRef<GitChangeTree | null>(null);
  const seenReloadNonce = useRef(reloadNonce);
  treeRef.current = tree;

  useEffect(() => {
    mutateGate.begin();
    mutating.current = false;
    setBusy(false);
  }, [server, workspaceId, mutateGate]);

  const selectionWorkspace = useRef<string | null>(null);

  const applyTree = useCallback((changes: GitChangeTree | null) => {
    setTree(changes);
    const flattened = flattenChangeTree(changes);
    setSelected(current => {
      const keep = selectionWorkspace.current === workspaceId ? current : [];
      selectionWorkspace.current = workspaceId;
      return retainSelectedPaths(flattened, keep);
    });
    setCollapsed(current => retainCollapsedPaths(flattened, current));
    setStale(false);
    setConfirm(null);
  }, [workspaceId]);

  const load = useCallback(async (silent = false, apply = !silent) => {
    if (silent && inflightLoads.current > 0) return;
    const ticket = treeGate.begin();
    if (!server || !workspaceId) {
      selectionWorkspace.current = null;
      setTree(null);
      setQueue(null);
      setSelected([]);
      setCollapsed([]);
      setStale(false);
      setConfirm(null);
      return;
    }
    if (!silent) { setLoading(true); setError(null); }
    inflightLoads.current += 1;
    try {
      const changes = await getChanges(server, workspaceId);
      if (!treeGate.isCurrent(ticket)) return;
      const visualized = flattenChangeTree(treeRef.current);
      const incoming = flattenChangeTree(changes);
      if (!apply && silent && treeRef.current !== null && isChangeTreeStale(visualized, incoming)) {
        setStale(true);
        setConfirm(current => current?.copy.cancel === false
          ? current
          : { copy: gitStaleTreeConfirmCopy(), action: () => { void load(false, true); } });
        return;
      }
      applyTree(changes);
      if (silent) setError(null);
      if (overview) return;
      try {
        const proposals = await getCommitQueue(server, workspaceId);
        if (!treeGate.isCurrent(ticket)) return;
        setQueue(proposals);
      } catch (cause) {
        if (!treeGate.isCurrent(ticket)) return;
        if (isUnauthorized(cause)) {
          expireActiveCredential();
          return;
        }
        setQueue(null);
        setError(cause instanceof Error ? cause.message : 'Could not load the agent proposal queue.');
      }
    } catch (cause) {
      if (!treeGate.isCurrent(ticket)) return;
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      if (!silent) setTree(null);
      if (!silent) setQueue(null);
      setError(cause instanceof Error ? cause.message : 'Could not load Git changes.');
    } finally {
      inflightLoads.current = Math.max(0, inflightLoads.current - 1);
      if (treeGate.isCurrent(ticket) && !silent) setLoading(false);
    }
  }, [server, workspaceId, treeGate, expireActiveCredential, overview, applyTree]);

  useEffect(() => { void load(false, true); }, [load]);
  useEffect(() => {
    if (!server || !workspaceId) return;
    const timer = setInterval(() => { void load(true, false); }, POLL_MS);
    return () => clearInterval(timer);
  }, [server, workspaceId, load]);
  useEffect(() => {
    if (reloadNonce === seenReloadNonce.current) return;
    seenReloadNonce.current = reloadNonce;
    void load(false, true);
  }, [reloadNonce, load]);

  const openDiff = async (node: GitChangeNode) => {
    if (!server || !workspaceId || node.isDirectory) return;
    onOpenFile?.(node.path);
    const ticket = diffGate.begin();
    setDiffPath(node.path); setDiff(null); setDiffLoading(true);
    try {
      const loaded = await getFileDiff(server, workspaceId, node.path);
      if (!diffGate.isCurrent(ticket)) return;
      setDiff(loaded);
    } catch (cause) {
      if (!diffGate.isCurrent(ticket)) return;
      setDiff({ path: node.path, status: 'Modified', isBinary: false, diff: cause instanceof Error ? cause.message : 'Could not load the diff.' });
    } finally {
      if (diffGate.isCurrent(ticket)) setDiffLoading(false);
    }
  };

  const runMutation = async (action: () => Promise<{ succeeded: boolean; error?: string | null; commit?: string | null }>, onSuccess: (result: { commit?: string | null }) => string) => {
    if (!server || !workspaceId || busy || mutating.current || stale) return false;
    mutating.current = true;
    const ticket = mutateGate.current();
    setBusy(true); setError(null); setStatus(null);
    try {
      const result = await action();
      if (!mutateGate.isCurrent(ticket)) return false;
      if (!result.succeeded) { setError(result.error ?? 'The Git operation failed.'); return false; }
      setStatus(onSuccess(result));
      await load(true, true);
      return true;
    } catch (cause) {
      if (!mutateGate.isCurrent(ticket)) return false;
      setError(cause instanceof Error ? cause.message : 'The Git operation failed.');
      return false;
    } finally {
      mutating.current = false;
      if (mutateGate.isCurrent(ticket)) setBusy(false);
    }
  };

  const selectedCount = selectedFilePaths(nodes, selected).length;
  const fileCount = nodes.filter(node => !node.isDirectory).length;
  const files = selectedFilePaths(nodes, selected);
  const counts = selectedChangeStatusCounts(nodes, selected);
  const canCommit = !busy && !stale && canCommitSelection(selectedCount, message);
  const canDiscard = !overview && !stale && canDiscardSelection(selectedCount, busy);

  const commitSelection = () => void runMutation(async () => {
    const result = await commitFiles(server!, workspaceId!, files, message.trim());
    if (result.succeeded) setMessage('');
    return result;
  }, result => `Committed ${files.length} file(s) as ${(result.commit ?? 'HEAD').slice(0, 8)}.`);

  const confirmCommit = () => {
    if (!canCommit) return;
    setConfirm({ copy: gitCommitConfirmCopy(message, files), action: commitSelection });
  };

  const countLabel = [
    counts.added > 0 ? `+${counts.added}` : null,
    counts.deleted > 0 ? `−${counts.deleted}` : null,
  ].filter(Boolean).join(' ');

  const commitButton = (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={countLabel ? `Commit ${countLabel}` : 'Commit'}
      disabled={!canCommit}
      onPress={confirmCommit}
      style={[styles.button, !canCommit && styles.disabled]}>
      <Text style={styles.buttonText}>Commit</Text>
      {counts.added > 0 && <Text style={styles.insertionsOnPrimary}>+{counts.added}</Text>}
      {counts.deleted > 0 && <Text style={styles.deletionsOnPrimary}>−{counts.deleted}</Text>}
    </Pressable>
  );

  return (
    <View style={styles.panel}>
      {!overview &&
        <View style={styles.header}>
          <View style={styles.titleRow}>
            <Text style={styles.summary}>{selectedCount} of {fileCount} file(s) selected</Text>
            <Pressable disabled={!canDiscard} onPress={() => {
              setConfirm({
                copy: gitDiscardConfirmCopy(files),
                action: () => void runMutation(() => discardFiles(server!, workspaceId!, files), () => `Discarded ${files.length} file(s).`),
              });
            }}
              style={[styles.discardButton, !canDiscard && styles.disabled]}>
              <Text style={styles.discardText}>Discard</Text>
            </Pressable>
          </View>
        </View>}

      {!overview && !!queue?.entries.length &&
        <View accessibilityLabel="Agent proposal queue" style={styles.queue}>
          <Text style={styles.queueTitle}>Agent proposal queue · generation {queue.generation}</Text>
          {queue.entries.map((entry, index) =>
            <View key={entry.id} style={styles.queueEntry}>
              <Text numberOfLines={1} style={styles.queueMessage}>{index + 1}. {entry.message}</Text>
              <Text style={styles.queueState}>{entry.state}</Text>
            </View>)}
        </View>}

      {loading && <ActivityIndicator color={agentUpTheme.colors.accentSoft} />}
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
      {!!status && <Text style={styles.status}>{status}</Text>}

      <ScrollView style={overview ? styles.overviewTree : styles.tree} contentContainerStyle={styles.treeContent} keyboardShouldPersistTaps="handled">
        {!loading && !error && nodes.length === 0 && selectedWorkspace &&
          <Text style={styles.empty}>No uncommitted changes.</Text>}
        {visible.map(node => {
          const checked = node.isDirectory
            ? isDirectorySelected(nodes, node, selected)
            : selected.includes(node.path);
          return (
            <GitChangeRow
              key={node.key}
              node={node}
              checked={checked}
              open={!node.isDirectory && diffPath === node.path}
              expanded={!collapsed.includes(node.path)}
              onToggle={() => setSelected(current => toggleNodeSelection(nodes, node, current))}
              onToggleExpand={() => setCollapsed(current => toggleDirectoryCollapsed(current, node.path))}
              onOpenFile={() => void openDiff(node)}
            />
          );
        })}
      </ScrollView>

      <View style={styles.footer}>
        <Text style={styles.label}>Commit message</Text>
        <TextInput accessibilityLabel="Commit message" multiline value={message} onChangeText={setMessage}
          editable={!busy} placeholder="fix(App): correct the port probe" placeholderTextColor={agentUpTheme.colors.textFaint}
          style={overview ? styles.overviewMessage : styles.messageInput} />
        <View style={styles.actions}>{commitButton}</View>
      </View>

      <GitConfirmDialog
        copy={confirm?.copy ?? null}
        busy={busy}
        onCancel={() => { if (confirm?.copy.cancel !== false) setConfirm(null); }}
        onConfirm={() => {
          const next = confirm;
          setConfirm(null);
          next?.action();
        }}
      />

      <Modal visible={diffPath !== null} transparent animationType="fade" onRequestClose={() => setDiffPath(null)}>
        <View style={styles.modalScrim}>
          {diffPath !== null &&
            <GitFileViewer
              path={diffPath}
              status={diff?.status ?? ''}
              diff={diff}
              loading={diffLoading}
              onClose={() => setDiffPath(null)}
            />}
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  panel: { flex: 1, gap: 12 },
  header: { gap: 4, paddingBottom: 4 },
  titleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  discardButton: { ...auBox('button', 'buttonDanger', 'buttonCompact'), minHeight: 32, paddingHorizontal: 10, alignItems: 'center', justifyContent: 'center' },
  discardText: auText('button', 'buttonCompact'),
  summary: auText('muted'),
  queue: { ...auBox('card'), gap: 6, padding: 10 },
  queueTitle: { ...auText('accent'), fontSize: 12, fontWeight: '800' },
  queueEntry: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  queueMessage: { ...auText('workspaceName'), flex: 1, fontSize: 12 },
  queueState: { ...auText('accent'), fontSize: 11, fontWeight: '700' },
  empty: { ...auText('muted'), lineHeight: 21 },
  error: { ...auText('badgeDanger'), lineHeight: 21 },
  status: { ...auText('accent'), lineHeight: 21 },
  tree: { flex: 1, minHeight: 80, ...auBox('gitChangeList') },
  overviewTree: { flex: 1, minHeight: 120, ...auBox('gitChangeList') },
  treeContent: { paddingVertical: 6, flexGrow: 1 },
  footer: { gap: 12, zIndex: 2 },
  actions: { flexDirection: 'row', gap: 8 },
  label: auText('fieldLabel'),
  messageInput: { minHeight: 90, ...auBox('input'), ...auText('input'), textAlignVertical: 'top' },
  overviewMessage: { minHeight: 72, ...auBox('input'), ...auText('input'), textAlignVertical: 'top' },
  button: { ...auBox('button'), flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8 },
  buttonText: auText('button'),
  insertionsOnPrimary: auText('gitInsertions', 'gitInsertionsOnPrimary'),
  deletionsOnPrimary: auText('gitDeletions', 'gitDeletionsOnPrimary'),
  disabled: { opacity: 0.38 },
  modalScrim: { flex: 1, padding: 20, alignItems: 'center', justifyContent: 'center', ...auBox('scrim') },
});
