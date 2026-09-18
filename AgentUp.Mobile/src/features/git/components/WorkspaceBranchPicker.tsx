import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { GitHeadState, GitRemoteBranch } from '../models/GitChanges';
import { checkoutRemote, fetchRemote, getHeadState, pullRemote, pushRemote, switchBranch } from '../providers/GitApiProvider';

type WorkspaceBranchPickerProps = {
  workspaceId: string;
};

export function WorkspaceBranchPicker({ workspaceId }: WorkspaceBranchPickerProps) {
  const { expireActiveCredential } = useServers();
  const { server, refresh } = useWorkspaces();
  const [head, setHead] = useState<GitHeadState | null>(null);
  const [open, setOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const request = useRef(0);

  const load = useCallback(async () => {
    if (!server) return;
    const ticket = ++request.current;
    try {
      const next = await getHeadState(server, workspaceId);
      if (ticket !== request.current) return;
      setHead(next);
    } catch (cause) {
      if (ticket !== request.current) return;
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not load branches.');
    }
  }, [server, workspaceId, expireActiveCredential]);

  useEffect(() => {
    setHead(null);
    setOpen(false);
    setCreating(false);
    setName('');
    setError(null);
    void load();
  }, [load]);

  const afterSuccess = async () => {
    setCreating(false);
    setOpen(false);
    setName('');
    await load();
    await refresh();
  };

  const runSwitch = async (next: string, create: boolean) => {
    if (!server || busy || next.length === 0 || next === head?.branch) return;
    setBusy(true);
    setError(null);
    try {
      const result = await switchBranch(server, workspaceId, next, create);
      if (!result.succeeded) {
        setError(result.error ?? 'The branch switch failed.');
        return;
      }
      await afterSuccess();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'The branch switch failed.');
    } finally {
      setBusy(false);
    }
  };

  const runCheckout = async (next: string) => {
    if (!server || busy || next.length === 0) return;
    setBusy(true);
    setError(null);
    try {
      const result = await checkoutRemote(server, workspaceId, next);
      if (!result.succeeded) {
        setError(result.error ?? 'The checkout failed.');
        return;
      }
      await afterSuccess();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'The checkout failed.');
    } finally {
      setBusy(false);
    }
  };

  const runSync = async (label: string, action: () => Promise<{ succeeded: boolean; error?: string | null }>) => {
    if (!server || busy) return;
    setBusy(true);
    setError(null);
    try {
      const result = await action();
      if (!result.succeeded) {
        setError(result.error ?? `The ${label} failed.`);
        return;
      }
      await afterSuccess();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : `The ${label} failed.`);
    } finally {
      setBusy(false);
    }
  };

  const confirmForcePush = () => {
    Alert.alert(
      'Force-push with lease?',
      'This updates the remote branch only if nobody else has pushed since your last fetch.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Force push',
          style: 'destructive',
          onPress: () => void runSync('force push', () => pushRemote(server!, workspaceId, true, false)),
        },
      ],
    );
  };

  const branch = head?.branch || 'not on a git branch';
  const branches = head?.localBranches ?? [];
  const remotes = head?.remoteBranches ?? [];
  const ahead = head?.ahead ?? 0;
  const behind = head?.behind ?? 0;
  const sync = ahead === 0 && behind === 0 ? '' : `↑${ahead} ↓${behind}`;

  return (
    <View style={styles.wrap}>
      <View style={styles.row}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`Current branch ${branch}`}
          disabled={busy || (branches.length === 0 && remotes.length === 0)}
          onPress={() => { setCreating(false); setOpen(value => !value); }}
          style={styles.dropdown}>
          <Text numberOfLines={1} style={styles.branch}>{branch}</Text>
          <Text style={styles.chevron}>{open ? '▴' : '▾'}</Text>
        </Pressable>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Create branch"
          disabled={busy}
          onPress={() => { setOpen(false); setCreating(true); }}
          style={styles.plus}>
          <Text style={styles.plusText}>+</Text>
        </Pressable>
      </View>
      {!!sync && <Text style={styles.sync}>{sync}</Text>}
      <View style={styles.actions}>
        <Pressable disabled={busy} onPress={() => void runSync('fetch', () => fetchRemote(server!, workspaceId, null))} style={styles.action}>
          <Text style={styles.actionText}>Fetch</Text>
        </Pressable>
        <Pressable disabled={busy} onPress={() => void runSync('pull', () => pullRemote(server!, workspaceId, false))} style={styles.action}>
          <Text style={styles.actionText}>Pull</Text>
        </Pressable>
        <Pressable disabled={busy} onPress={() => void runSync('push', () => pushRemote(server!, workspaceId, false, false))} style={styles.actionPrimary}>
          <Text style={styles.actionPrimaryText}>Push</Text>
        </Pressable>
        <Pressable disabled={busy} onPress={confirmForcePush} style={styles.actionDanger}>
          <Text style={styles.actionDangerText}>Force push</Text>
        </Pressable>
      </View>
      {open && (branches.length > 0 || remotes.length > 0) && (
        <View style={styles.menu}>
          {branches.length > 0 && <Text style={styles.section}>Local</Text>}
          {branches.map(item => (
            <Pressable
              key={`local:${item}`}
              disabled={busy || item === head?.branch}
              onPress={() => void runSwitch(item, false)}
              style={[styles.option, item === head?.branch && styles.optionActive]}>
              <Text style={[styles.optionText, item === head?.branch && styles.optionTextActive]}>{item}</Text>
            </Pressable>
          ))}
          {remotes.length > 0 && <Text style={styles.section}>Remote</Text>}
          {remotes.map(item => (
            <Pressable
              key={`remote:${item.remote}/${item.name}`}
              disabled={busy}
              onPress={() => void runCheckout(`${item.remote}/${item.name}`)}
              style={styles.option}>
              <Text style={styles.optionText}>{formatRemote(item)}</Text>
            </Pressable>
          ))}
        </View>
      )}
      {creating && (
        <View style={styles.create}>
          <TextInput
            accessibilityLabel="New branch name"
            value={name}
            onChangeText={setName}
            placeholder="new-branch"
            placeholderTextColor={agentUpTheme.colors.textFaint}
            style={styles.input}
          />
          <Pressable
            disabled={busy || !name.trim()}
            onPress={() => void runSwitch(name.trim(), true)}
            style={[styles.createButton, (!name.trim() || busy) && styles.disabled]}>
            <Text style={styles.createButtonText}>Create</Text>
          </Pressable>
          <Pressable onPress={() => { setCreating(false); setName(''); }} style={styles.cancel}>
            <Text style={styles.cancelText}>Cancel</Text>
          </Pressable>
        </View>
      )}
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
    </View>
  );
}

function formatRemote(item: GitRemoteBranch): string {
  return `${item.remote}/${item.name}`;
}

const styles = StyleSheet.create({
  wrap: { gap: 8 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  dropdown: {
    flex: 1,
    minHeight: 40,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
    paddingHorizontal: 12,
    ...auBox('input'),
  },
  branch: { flex: 1, ...auText('workspaceName') },
  chevron: auText('muted'),
  plus: {
    ...auBox('workspaceAdd'),
    width: 40,
    height: 40,
    alignItems: 'center',
    justifyContent: 'center',
  },
  plusText: { ...auText('accent'), fontSize: 20, fontWeight: '700', lineHeight: 22 },
  sync: auText('muted'),
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  action: { ...auBox('button', 'buttonSecondary', 'buttonCompact'), minHeight: 32, paddingHorizontal: 10, alignItems: 'center', justifyContent: 'center' },
  actionText: auText('buttonSecondary', 'buttonCompact'),
  actionPrimary: { ...auBox('button', 'buttonCompact'), minHeight: 32, paddingHorizontal: 10, alignItems: 'center', justifyContent: 'center' },
  actionPrimaryText: auText('button', 'buttonCompact'),
  actionDanger: { ...auBox('button', 'buttonDanger', 'buttonCompact'), minHeight: 32, paddingHorizontal: 10, alignItems: 'center', justifyContent: 'center' },
  actionDangerText: auText('button', 'buttonCompact'),
  menu: { ...auBox('card'), overflow: 'hidden', paddingHorizontal: 0, paddingVertical: 0 },
  section: { ...auText('fieldLabel'), paddingHorizontal: 12, paddingTop: 8, paddingBottom: 4 },
  option: { paddingHorizontal: 12, paddingVertical: 10 },
  optionActive: auBox('cardSelected'),
  optionText: auText('muted'),
  optionTextActive: auText('workspaceName'),
  create: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  input: { flex: 1, ...auBox('input'), ...auText('input') },
  createButton: { ...auBox('button', 'buttonCompact'), alignItems: 'center', justifyContent: 'center' },
  createButtonText: auText('button', 'buttonCompact'),
  cancel: { minHeight: 40, paddingHorizontal: 10, alignItems: 'center', justifyContent: 'center' },
  cancelText: auText('buttonSecondary', 'buttonCompact'),
  disabled: { opacity: 0.38 },
  error: { ...auText('badgeDanger'), lineHeight: 21 },
});
