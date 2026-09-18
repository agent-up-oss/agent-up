import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { GitLogRow } from '../models/GitChanges';
import { checkoutRemote, getHeadState, getLog, switchBranch } from '../providers/GitApiProvider';
import { createRequestGate } from '../providers/RequestGateProvider';
import { layoutGitLog } from '../providers/GitLogLayoutProvider';

const POLL_MS = 2500;

export function GitHistoryPanel({ workspaceId }: { workspaceId: string }) {
  const { expireActiveCredential } = useServers();
  const { server } = useWorkspaces();
  const [rows, setRows] = useState<GitLogRow[]>([]);
  const [locals, setLocals] = useState<string[]>([]);
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
        {rows.map(row =>
          <View key={row.commit.id} style={styles.row}>
            <Text style={styles.graph}>{row.graph}</Text>
            <Pressable
              disabled={busy || !row.checkoutName}
              onPress={() => { if (row.checkoutName) void checkout(row.checkoutName); }}>
              <Text style={styles.hash}>{row.commit.shortId}</Text>
            </Pressable>
            <Text numberOfLines={1} style={styles.subject}>{row.commit.subject}</Text>
          </View>)}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  panel: { flex: 1, gap: 8 },
  list: { gap: 8, paddingVertical: 8 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  graph: { ...auText('mono', 'muted'), fontSize: agentUpTheme.typography.sizeXs },
  hash: auText('accent'),
  subject: { ...auText('workspaceName'), flex: 1, fontSize: 12 },
  empty: auText('muted'),
  error: auText('badgeDanger'),
  status: auText('accent'),
});
