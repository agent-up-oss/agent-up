import { useCallback, useEffect, useRef, useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { GitHeadState } from '../models/GitChanges';
import { getHeadState, switchBranch } from '../providers/GitApiProvider';

type WorkspaceBranchPickerProps = {
  workspaceId: string;
};

export function WorkspaceBranchPicker({ workspaceId }: WorkspaceBranchPickerProps) {
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
      setError(cause instanceof Error ? cause.message : 'Could not load branches.');
    }
  }, [server, workspaceId]);

  useEffect(() => {
    setHead(null);
    setOpen(false);
    setCreating(false);
    setName('');
    setError(null);
    void load();
  }, [load]);

  const run = async (next: string, create: boolean) => {
    if (!server || busy || next.length === 0 || next === head?.branch) return;
    setBusy(true);
    setError(null);
    try {
      const result = await switchBranch(server, workspaceId, next, create);
      if (!result.succeeded) {
        setError(result.error ?? 'The branch switch failed.');
        return;
      }
      setCreating(false);
      setOpen(false);
      setName('');
      await load();
      await refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'The branch switch failed.');
    } finally {
      setBusy(false);
    }
  };

  const branch = head?.branch || 'not on a git branch';
  const branches = head?.localBranches ?? [];

  return (
    <View style={styles.wrap}>
      <View style={styles.row}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`Current branch ${branch}`}
          disabled={busy || branches.length === 0}
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
      {open && branches.length > 0 && (
        <View style={styles.menu}>
          {branches.map(item => (
            <Pressable
              key={item}
              disabled={busy || item === head?.branch}
              onPress={() => void run(item, false)}
              style={[styles.option, item === head?.branch && styles.optionActive]}>
              <Text style={[styles.optionText, item === head?.branch && styles.optionTextActive]}>{item}</Text>
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
            onPress={() => void run(name.trim(), true)}
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
  menu: { ...auBox('card'), overflow: 'hidden', paddingHorizontal: 0, paddingVertical: 0 },
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
