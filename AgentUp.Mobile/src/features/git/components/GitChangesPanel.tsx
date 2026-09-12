import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { GitChangeNode, GitChangeTree, GitFileDiff } from '../models/GitChanges';
import { commitFiles, discardFiles, getChanges, getFileDiff } from '../providers/GitApiProvider';
import { createRequestGate, type RequestGate } from '../providers/RequestGateProvider';
import {
  canCommitSelection,
  canDiscardSelection,
  flattenChangeTree,
  isDirectorySelected,
  retainSelectedPaths,
  selectedFilePaths,
  statusColor,
  statusGlyph,
  toggleNodeSelection,
} from '../providers/GitChangeTreeProvider';

const POLL_MS = 2500;

export function GitChangesPanel() {
  const { server, selectedWorkspace } = useWorkspaces();
  const workspaceId = selectedWorkspace?.id ?? null;

  const [tree, setTree] = useState<GitChangeTree | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [diff, setDiff] = useState<GitFileDiff | null>(null);
  const [diffPath, setDiffPath] = useState<string | null>(null);
  const [diffLoading, setDiffLoading] = useState(false);

  const nodes = useMemo(() => flattenChangeTree(tree), [tree]);

  const gates = useRef<{ tree: RequestGate; diff: RequestGate; mutate: RequestGate } | null>(null);
  gates.current ??= { tree: createRequestGate(), diff: createRequestGate(), mutate: createRequestGate() };
  const { tree: treeGate, diff: diffGate, mutate: mutateGate } = gates.current;

  useEffect(() => { mutateGate.begin(); }, [server, workspaceId, mutateGate]);

  const selectionWorkspace = useRef<string | null>(null);

  const load = useCallback(async (silent = false) => {
    const ticket = treeGate.begin();
    if (!server || !workspaceId) {
      selectionWorkspace.current = null;
      setTree(null);
      setSelected([]);
      return;
    }
    if (!silent) { setLoading(true); setError(null); }
    try {
      const changes = await getChanges(server, workspaceId);
      if (!treeGate.isCurrent(ticket)) return;
      setTree(changes);
      setSelected(current => {
        const keep = selectionWorkspace.current === workspaceId ? current : [];
        selectionWorkspace.current = workspaceId;
        return retainSelectedPaths(flattenChangeTree(changes), keep);
      });
      if (silent) setError(null);
    } catch (cause) {
      if (!treeGate.isCurrent(ticket)) return;
      if (!silent) setTree(null);
      setError(cause instanceof Error ? cause.message : 'Could not load Git changes.');
    } finally {
      if (treeGate.isCurrent(ticket) && !silent) setLoading(false);
    }
  }, [server, workspaceId, treeGate]);

  useEffect(() => { void load(false); }, [load]);
  useEffect(() => {
    if (!server || !workspaceId) return;
    const timer = setInterval(() => { void load(true); }, POLL_MS);
    return () => clearInterval(timer);
  }, [server, workspaceId, load]);

  const openDiff = async (node: GitChangeNode) => {
    if (!server || !workspaceId || node.isDirectory) return;
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
    if (!server || !workspaceId || busy) return false;
    const ticket = mutateGate.current();
    setBusy(true); setError(null); setStatus(null);
    try {
      const result = await action();
      if (!mutateGate.isCurrent(ticket)) return false;
      if (!result.succeeded) { setError(result.error ?? 'The Git operation failed.'); return false; }
      setStatus(onSuccess(result));
      await load(true);
      return true;
    } catch (cause) {
      if (!mutateGate.isCurrent(ticket)) return false;
      setError(cause instanceof Error ? cause.message : 'The Git operation failed.');
      return false;
    } finally {
      if (mutateGate.isCurrent(ticket)) setBusy(false);
    }
  };

  const selectedCount = selectedFilePaths(nodes, selected).length;
  const fileCount = nodes.filter(node => !node.isDirectory).length;
  const files = selectedFilePaths(nodes, selected);
  const canCommit = !busy && canCommitSelection(selectedCount, message);
  const canDiscard = canDiscardSelection(selectedCount, busy);

  return (
    <View style={styles.panel}>
      <View style={styles.header}>
        <View style={styles.titleRow}>
          <Text style={styles.summary}>{selectedCount} of {fileCount} file(s) selected</Text>
          <Pressable disabled={!canDiscard} onPress={() => void runMutation(() => discardFiles(server!, workspaceId!, files), () => `Discarded ${files.length} file(s).`)}
            style={[styles.discardButton, !canDiscard && styles.disabled]}>
            <Text style={styles.discardText}>Discard</Text>
          </Pressable>
        </View>
      </View>

      {loading && <ActivityIndicator color="#00d66b" />}
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
      {!!status && <Text style={styles.status}>{status}</Text>}

      <ScrollView style={styles.tree} contentContainerStyle={styles.treeContent} keyboardShouldPersistTaps="handled">
        {!loading && !error && nodes.length === 0 && selectedWorkspace &&
          <Text style={styles.empty}>No uncommitted changes.</Text>}
        {nodes.map(node => {
          const checked = node.isDirectory
            ? isDirectorySelected(nodes, node, selected)
            : selected.includes(node.path);
          return (
            <View key={node.key} style={[styles.row, { paddingLeft: 4 + node.depth * 16 }]}>
              <Pressable accessibilityRole="checkbox" accessibilityState={{ checked }}
                accessibilityLabel={`Select ${node.path}`}
                onPress={() => setSelected(current => toggleNodeSelection(nodes, node, current))}
                style={[styles.checkbox, checked && styles.checkboxChecked]}>
                <Text style={styles.checkmark}>{checked ? '✓' : ''}</Text>
              </Pressable>
              <Text style={[styles.glyph, { color: statusColor(node.status) }]}>{statusGlyph(node.status)}</Text>
              <Pressable accessibilityRole="button" accessibilityLabel={`Open ${node.path}`}
                disabled={node.isDirectory} onPress={() => void openDiff(node)} style={styles.nameButton}>
                <Text numberOfLines={1} style={node.isDirectory ? styles.directoryName : styles.fileName}>{node.name}</Text>
              </Pressable>
            </View>
          );
        })}
      </ScrollView>

      <View style={styles.footer}>
      <Text style={styles.label}>Commit message</Text>
      <TextInput accessibilityLabel="Commit message" multiline value={message} onChangeText={setMessage}
        editable={!busy} placeholder="fix(App): correct the port probe" placeholderTextColor="#718077"
        style={styles.messageInput} />

      <Pressable accessibilityRole="button" accessibilityLabel="Commit" disabled={!canCommit}
        onPress={() => void runMutation(async () => { const result = await commitFiles(server!, workspaceId!, files, message.trim()); if (result.succeeded) setMessage(''); return result; }, result => `Committed ${files.length} file(s) as ${(result.commit ?? 'HEAD').slice(0, 8)}.`)}
        style={[styles.button, !canCommit && styles.disabled]}>
        <Text style={styles.buttonText}>Commit</Text>
      </Pressable>
      </View>

      <Modal visible={diffPath !== null} transparent animationType="fade" onRequestClose={() => setDiffPath(null)}>
        <View style={styles.modalScrim}>
          <View style={styles.dialog}>
            <Text accessibilityRole="header" numberOfLines={2} style={styles.dialogTitle}>{diffPath}</Text>
            {diffLoading
              ? <ActivityIndicator color="#00d66b" />
              : <ScrollView horizontal style={styles.diffScroll}>
                  <ScrollView>
                    <Text style={styles.diffText}>{diffText(diff)}</Text>
                  </ScrollView>
                </ScrollView>}
            <Pressable accessibilityRole="button" accessibilityLabel="Close diff" onPress={() => setDiffPath(null)}
              style={styles.secondaryButton}>
              <Text style={styles.secondaryButtonText}>Close</Text>
            </Pressable>
          </View>
        </View>
      </Modal>
    </View>
  );
}

function diffText(diff: GitFileDiff | null): string {
  if (!diff) return 'This file no longer has changes.';
  if (diff.isBinary) return 'This file is binary; Agent-Up does not render a text diff for it.';
  return diff.diff;
}

const styles = StyleSheet.create({
  panel: { flex: 1, gap: 12 },
  header: { gap: 4, paddingBottom: 4 },
  titleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  discardButton: { minHeight: 32, paddingHorizontal: 10, alignItems: 'center', justifyContent: 'center', borderRadius: 8, borderWidth: 1, borderColor: '#8d3c3c' },
  discardText: { color: '#e48989', fontWeight: '700', fontSize: 12 },
  summary: { color: '#789085', fontSize: 12 },
  empty: { color: '#aebcb3', lineHeight: 21 },
  error: { color: '#d84f4f', lineHeight: 21 },
  status: { color: '#2bf27a', lineHeight: 21 },
  tree: { flex: 1, minHeight: 80, borderRadius: 8, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505' },
  treeContent: { paddingVertical: 6, flexGrow: 1 },
  footer: { gap: 12 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingRight: 10, paddingVertical: 5 },
  checkbox: { width: 20, height: 20, borderRadius: 4, borderWidth: 1, borderColor: '#287038', alignItems: 'center', justifyContent: 'center' },
  checkboxChecked: { backgroundColor: '#0f7a45', borderColor: '#2bf27a' },
  checkmark: { color: '#f5fbf7', fontSize: 12, lineHeight: 14 },
  glyph: { width: 14, fontSize: 12, fontWeight: '800' },
  nameButton: { flexShrink: 1 },
  directoryName: { color: '#9fb2a8', fontSize: 14, fontWeight: '700' },
  fileName: { color: '#f5fbf7', fontSize: 14 },
  label: { color: '#f5fbf7', fontWeight: '700' },
  messageInput: { minHeight: 90, borderRadius: 8, borderWidth: 1, borderColor: '#287038', padding: 12, color: '#f5fbf7', backgroundColor: '#080808', textAlignVertical: 'top' },
  button: { minHeight: 48, alignItems: 'center', justifyContent: 'center', borderRadius: 8, backgroundColor: '#00d66b' },
  buttonText: { color: '#000000', fontWeight: '800' },
  secondaryButton: { minHeight: 44, paddingHorizontal: 22, alignItems: 'center', justifyContent: 'center', borderRadius: 8, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505' },
  secondaryButtonText: { color: '#f5fbf7', fontWeight: '700' },
  disabled: { opacity: 0.38 },
  modalScrim: { flex: 1, padding: 20, alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.72)' },
  dialog: { width: '100%', maxWidth: 620, maxHeight: '85%', padding: 18, borderRadius: 10, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505', gap: 12 },
  dialogTitle: { color: '#f5fbf7', fontSize: 16, fontWeight: '800' },
  diffScroll: { flexGrow: 0 },
  diffText: { color: '#c6ddd2', fontSize: 12, fontFamily: 'monospace' },
});
