import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { GitLogRef, GitLogRow } from '../models/GitChanges';
import { checkoutRemote, getHeadState, getLog, switchBranch } from '../providers/GitApiProvider';
import { createRequestGate } from '../providers/RequestGateProvider';
import { formatGitLogTime, layoutGitLog } from '../providers/GitLogLayoutProvider';
import { GitLogGraphColumn } from './GitLogGraphColumn';

const POLL_MS = 2500;

export function GitHistoryPanel({ workspaceId }: { workspaceId: string }) {
  const { expireActiveCredential } = useServers();
  const { server } = useWorkspaces();
  const [rows, setRows] = useState<GitLogRow[]>([]);
  const [locals, setLocals] = useState<string[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const gate = useRef(createRequestGate());

  const load = useCallback(async (silent = false) => {
    if (!server) return;
    const ticket = gate.current.begin();
    if (!silent) setLoading(true);
    try {
      const [history, head] = await Promise.all([getLog(server, workspaceId), getHeadState(server, workspaceId)]);
      if (!gate.current.isCurrent(ticket)) return;
      setRows(layoutGitLog(history?.commits));
      setLocals(head?.localBranches ?? []);
      setError(null);
    } catch (cause) {
      if (!gate.current.isCurrent(ticket)) return;
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not load Git history.');
    } finally {
      if (gate.current.isCurrent(ticket) && !silent) setLoading(false);
    }
  }, [server, workspaceId, expireActiveCredential]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    const timer = setInterval(() => { void load(true); }, POLL_MS);
    return () => clearInterval(timer);
  }, [load]);

  const checkout = async (name: string) => {
    if (!server || busy) return;
    setBusy(true);
    setError(null);
    try {
      const result = locals.includes(name)
        ? await switchBranch(server, workspaceId, name, false)
        : await checkoutRemote(server, workspaceId, name);
      if (!result.succeeded) {
        setError(result.error ?? 'The checkout failed.');
        return;
      }
      setStatus(`Checked out ${name}.`);
      await load(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'The checkout failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.panel}>
      {loading && <ActivityIndicator color={agentUpTheme.colors.accentSoft} />}
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
      {!!status && <Text style={styles.status}>{status}</Text>}
      <ScrollView contentContainerStyle={styles.list} keyboardShouldPersistTaps="handled">
        {rows.length === 0 && !loading && <Text style={styles.empty}>No commits yet.</Text>}
        {rows.map(row => {
          const selected = row.commit.id === selectedId;
          return (
            <Pressable
              key={row.commit.id}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              onPress={() => setSelectedId(row.commit.id)}
              style={[styles.row, selected ? styles.rowSelected : null]}>
              <GitLogGraphColumn row={row} />
              <View style={styles.body}>
                {row.refs.length > 0 &&
                  <View style={styles.refs}>
                    {row.refs.map(ref =>
                      <GitLogRefChip
                        key={`${row.commit.id}:${ref.name}`}
                        refInfo={ref}
                        disabled={busy || !canCheckout(ref, locals)}
                        onPress={() => { if (canCheckout(ref, locals)) void checkout(ref.name); }}
                      />)}
                  </View>}
                <Text numberOfLines={1} style={styles.subject}>{row.commit.subject}</Text>
                <Text numberOfLines={1} style={styles.author}>{row.commit.author}</Text>
                <Text style={styles.time}>{formatGitLogTime(row.commit.timestamp)}</Text>
              </View>
            </Pressable>
          );
        })}
      </ScrollView>
    </View>
  );
}

function GitLogRefChip({
  refInfo,
  disabled,
  onPress,
}: {
  refInfo: GitLogRef;
  disabled: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      disabled={disabled}
      onPress={onPress}
      style={[styles.ref, refInfo.kind === 'head' && styles.refHead, refInfo.kind === 'remote' && styles.refRemote]}>
      <Text
        numberOfLines={1}
        style={[
          styles.refLabel,
          refInfo.kind === 'head' && styles.refHeadLabel,
          refInfo.kind === 'remote' && styles.refRemoteLabel,
        ]}>
        {refInfo.name}
      </Text>
    </Pressable>
  );
}

function canCheckout(ref: GitLogRef, locals: string[]): boolean {
  if (ref.kind === 'head') return false;
  if (ref.kind === 'local') return locals.includes(ref.name);
  return ref.kind === 'remote';
}

const styles = StyleSheet.create({
  panel: { flex: 1, gap: 8 },
  list: { ...auBox('gitLog'), paddingVertical: 4 },
  row: {
    ...auBox('gitLogRow'),
    flexDirection: 'row',
    alignItems: 'center',
    gap: agentUpTheme.spacing[3],
  },
  rowSelected: auBox('gitLogRowSelected'),
  body: { flex: 1, flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: agentUpTheme.spacing[2], minWidth: 0 },
  refs: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, maxWidth: '100%' },
  ref: auBox('gitLogRef'),
  refHead: auBox('gitLogRefHead'),
  refRemote: auBox('gitLogRefRemote'),
  refLabel: auText('gitLogRef'),
  refHeadLabel: auText('gitLogRefHead'),
  refRemoteLabel: auText('gitLogRefRemote'),
  subject: { ...auText('gitLogSubject'), flexGrow: 1, flexShrink: 1 },
  author: auText('gitLogAuthor'),
  time: auText('gitLogTime'),
  empty: auText('muted'),
  error: auText('badgeDanger'),
  status: auText('accent'),
});
