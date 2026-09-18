import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { GitLogCommit, GitLogRef, GitLogRow } from '../models/GitChanges';
import { checkoutRemote, getHeadState, getLog, switchBranch } from '../providers/GitApiProvider';
import { createRequestGate } from '../providers/RequestGateProvider';
import {
  formatGitLogTimestamp,
  GIT_LOG_PAGE_SIZE,
  GIT_LOG_ROW_HEIGHT,
  GIT_LOG_TIME_WIDTH,
  layoutGitLog,
} from '../providers/GitLogLayoutProvider';
import { GitLogGraphColumn } from './GitLogGraphColumn';

const POLL_MS = 2500;

export function GitHistoryPanel({ workspaceId }: { workspaceId: string }) {
  const { expireActiveCredential } = useServers();
  const { server } = useWorkspaces();
  const [commits, setCommits] = useState<GitLogCommit[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [locals, setLocals] = useState<string[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const gate = useRef(createRequestGate());
  const commitsRef = useRef<GitLogCommit[]>([]);
  const pagedRef = useRef(false);

  const rows = useMemo(() => layoutGitLog(commits, locals), [commits, locals]);
  const selected = rows.find(row => row.commit.id === selectedId) ?? null;

  const applyPage = useCallback((page: GitLogCommit[] | undefined, more: boolean, append: boolean) => {
    const incoming = page ?? [];
    const next = append
      ? mergeCommits(commitsRef.current, incoming)
      : incoming;
    commitsRef.current = next;
    pagedRef.current = append || next.length > GIT_LOG_PAGE_SIZE;
    setCommits(next);
    setHasMore(more);
  }, []);

  const load = useCallback(async (silent = false) => {
    if (!server) return;
    const ticket = gate.current.begin();
    if (!silent) setLoading(true);
    try {
      const [history, head] = await Promise.all([
        getLog(server, workspaceId, GIT_LOG_PAGE_SIZE),
        getHeadState(server, workspaceId),
      ]);
      if (!gate.current.isCurrent(ticket)) return;
      if (!silent || !pagedRef.current) {
        applyPage(history?.commits, history?.hasMore ?? (history?.commits.length === GIT_LOG_PAGE_SIZE), false);
      }
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
  }, [server, workspaceId, expireActiveCredential, applyPage]);

  const loadMore = async () => {
    if (!server || loadingMore || !hasMore) return;
    if (commitsRef.current.length === 0) return;
    setLoadingMore(true);
    setError(null);
    try {
      const history = await getLog(server, workspaceId, GIT_LOG_PAGE_SIZE, commitsRef.current.length);
      applyPage(history?.commits, history?.hasMore ?? (history?.commits.length === GIT_LOG_PAGE_SIZE), true);
    } catch (cause) {
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not load older commits.');
    } finally {
      setLoadingMore(false);
    }
  };

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
      pagedRef.current = false;
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
      <View style={styles.log}>
        {selected && (
          <GitLogDetailHeader
            row={selected}
            locals={locals}
            disabled={busy}
            onCheckout={name => { void checkout(name); }}
          />
        )}
        <ScrollView style={styles.rows} contentContainerStyle={styles.list} keyboardShouldPersistTaps="handled">
        {rows.length === 0 && !loading && <Text style={styles.empty}>No commits yet.</Text>}
        {rows.length > 0 && (
          <View style={styles.track}>
            <View style={styles.times}>
              {rows.map(row => (
                <GitLogTimeCell
                  key={`t:${row.commit.id}`}
                  row={row}
                  selected={row.commit.id === selectedId}
                  onPress={() => setSelectedId(row.commit.id)}
                />
              ))}
            </View>
            <ScrollView
              horizontal
              nestedScrollEnabled
              style={styles.graphScroll}
              contentContainerStyle={styles.graphContent}>
              <View>
                {rows.map(row => (
                  <GitLogGraphCell
                    key={`g:${row.commit.id}`}
                    row={row}
                    selected={row.commit.id === selectedId}
                    onPress={() => setSelectedId(row.commit.id)}
                  />
                ))}
              </View>
            </ScrollView>
          </View>
        )}
        {hasMore && (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Load more commits"
            disabled={loadingMore}
            onPress={() => { void loadMore(); }}
            style={styles.loadMore}>
            {loadingMore
              ? <ActivityIndicator color={agentUpTheme.colors.accentSoft} />
              : <Text style={styles.loadMoreLabel}>Load more</Text>}
          </Pressable>
        )}
      </ScrollView>
      </View>
    </View>
  );
}

function GitLogDetailHeader({
  row,
  locals,
  disabled,
  onCheckout,
}: {
  row: GitLogRow;
  locals: string[];
  disabled: boolean;
  onCheckout: (name: string) => void;
}) {
  return (
    <View style={styles.detail}>
      <Text style={styles.subject}>{row.commit.subject}</Text>
      <Text style={styles.author}>{row.commit.author}</Text>
      {row.refs.length > 0 && (
        <View style={styles.refs}>
          {row.refs.map(ref =>
            <GitLogRefChip
              key={`${row.commit.id}:${ref.name}`}
              refInfo={ref}
              disabled={disabled || !canCheckout(ref, locals)}
              onPress={() => { if (canCheckout(ref, locals)) onCheckout(ref.name); }}
            />)}
        </View>
      )}
    </View>
  );
}

function GitLogTimeCell({
  row,
  selected,
  onPress,
}: {
  row: GitLogRow;
  selected: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${formatGitLogTimestamp(row.commit.timestamp)} ${row.commit.subject}`}
      accessibilityState={{ selected }}
      onPress={onPress}
      style={[styles.timeCell, selected ? styles.rowSelected : null]}>
      <Text numberOfLines={1} style={styles.time}>{formatGitLogTimestamp(row.commit.timestamp)}</Text>
    </Pressable>
  );
}

function GitLogGraphCell({
  row,
  selected,
  onPress,
}: {
  row: GitLogRow;
  selected: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={row.commit.subject}
      accessibilityState={{ selected }}
      onPress={onPress}
      style={[styles.graphCell, selected ? styles.rowSelected : null]}>
      <GitLogGraphColumn row={row} />
    </Pressable>
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
  const checkoutable = refInfo.kind === 'local' || refInfo.kind === 'remote';
  return (
    <Pressable
      disabled={disabled || !checkoutable}
      onPress={onPress}
      style={[
        styles.ref,
        refInfo.kind === 'head' && styles.refHead,
        refInfo.kind === 'remote' && styles.refRemote,
        refInfo.kind === 'tag' && styles.refTag,
      ]}>
      <Text
        numberOfLines={1}
        style={[
          styles.refLabel,
          refInfo.kind === 'head' && styles.refHeadLabel,
          refInfo.kind === 'remote' && styles.refRemoteLabel,
          refInfo.kind === 'tag' && styles.refTagLabel,
        ]}>
        {refInfo.name}
      </Text>
    </Pressable>
  );
}

function canCheckout(ref: GitLogRef, locals: string[]): boolean {
  if (ref.kind === 'head' || ref.kind === 'tag') return false;
  if (ref.kind === 'local') return locals.includes(ref.name);
  return ref.kind === 'remote';
}

function mergeCommits(existing: GitLogCommit[], incoming: GitLogCommit[]): GitLogCommit[] {
  const seen = new Set(existing.map(commit => commit.id));
  const extra = incoming.filter(commit => !seen.has(commit.id));
  return extra.length === 0 ? existing : [...existing, ...extra];
}

const styles = StyleSheet.create({
  panel: { flex: 1, gap: 8 },
  log: { ...auBox('gitLog'), flex: 1, minHeight: 0, overflow: 'hidden' },
  rows: { flex: 1, minHeight: 0 },
  list: { paddingVertical: 4, flexGrow: 1 },
  track: { flexDirection: 'row', alignItems: 'flex-start' },
  times: { width: GIT_LOG_TIME_WIDTH, flexShrink: 0 },
  graphScroll: { flex: 1, minWidth: 0 },
  graphContent: { flexGrow: 1 },
  timeCell: {
    ...auBox('gitLogRow'),
    width: GIT_LOG_TIME_WIDTH,
    height: GIT_LOG_ROW_HEIGHT,
    minHeight: GIT_LOG_ROW_HEIGHT,
    justifyContent: 'center',
    paddingHorizontal: agentUpTheme.spacing[2],
  },
  graphCell: {
    ...auBox('gitLogRow'),
    height: GIT_LOG_ROW_HEIGHT,
    minHeight: GIT_LOG_ROW_HEIGHT,
    justifyContent: 'center',
    paddingHorizontal: 0,
  },
  rowSelected: auBox('gitLogRowSelected'),
  detail: auBox('gitLogDetail'),
  refs: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, maxWidth: '100%' },
  ref: auBox('gitLogRef'),
  refHead: auBox('gitLogRefHead'),
  refRemote: auBox('gitLogRefRemote'),
  refTag: auBox('gitLogRefTag'),
  refLabel: auText('gitLogRef'),
  refHeadLabel: auText('gitLogRefHead'),
  refRemoteLabel: auText('gitLogRefRemote'),
  refTagLabel: auText('gitLogRefTag'),
  subject: auText('gitLogSubject'),
  author: auText('gitLogAuthor'),
  time: auText('gitLogTime'),
  empty: auText('muted'),
  error: auText('badgeDanger'),
  status: auText('accent'),
  loadMore: {
    ...auBox('button', 'buttonSecondary', 'buttonCompact'),
    alignItems: 'center',
    justifyContent: 'center',
    alignSelf: 'center',
    marginVertical: agentUpTheme.spacing[3],
  },
  loadMoreLabel: auText('buttonSecondary', 'buttonCompact'),
});
