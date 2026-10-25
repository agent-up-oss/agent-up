import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import {
  getPermissionPolicy,
  putPermissionPolicy,
  type PermissionRule,
  type PermissionRuleKind,
} from '@agent-up/chat';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useInnerShell } from '@/features/shell/hooks/useInnerShell';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';

const choices: { kind: PermissionRuleKind; title: string; detail: string }[] = [
  { kind: 'WorktreeRead', title: 'Read the worktree', detail: 'Inspect files in this workspace.' },
  { kind: 'WorktreeWrite', title: 'Write to the worktree', detail: 'Create, edit, and remove workspace files.' },
  { kind: 'Execute', title: 'Run commands', detail: 'Execute tools and development commands.' },
  { kind: 'Network', title: 'Use the network', detail: 'Contact remote services and package registries.' },
];

export function AgentPermissionPolicyScreen({ workspaceId }: { workspaceId: string }) {
  const router = useRouter();
  const { server } = useWorkspaces();
  const { expireActiveCredential } = useServers();
  const [allowed, setAllowed] = useState<Set<PermissionRuleKind>>(new Set());
  const [preservedRules, setPreservedRules] = useState<PermissionRule[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inner = useInnerShell('Autonomy permissions', `/(main)/workspace/${workspaceId}/agents`);
  useShellConfig(useMemo(() => inner, [inner]));

  const load = useCallback(async () => {
    if (!server) return;
    setLoading(true);
    try {
      const policy = await getPermissionPolicy(server, workspaceId);
      setAllowed(new Set((policy?.rules ?? []).filter(rule => rule.allow && !rule.scope).map(rule => rule.kind)));
      setPreservedRules((policy?.rules ?? []).filter(rule => !rule.allow || !!rule.scope));
      setError(null);
    } catch (cause) {
      if (isUnauthorized(cause)) { expireActiveCredential(); return; }
      setError(cause instanceof Error ? cause.message : 'Could not load the permission policy.');
    } finally {
      setLoading(false);
    }
  }, [expireActiveCredential, server, workspaceId]);

  useEffect(() => { void load(); }, [load]);

  const toggle = (kind: PermissionRuleKind) => setAllowed(current => {
    const next = new Set(current);
    if (next.has(kind)) next.delete(kind); else next.add(kind);
    return next;
  });

  const save = async () => {
    if (!server || busy) return;
    setBusy(true); setError(null);
    try {
      await putPermissionPolicy(server, workspaceId, {
        rules: [
          ...preservedRules,
          ...choices.filter(choice => allowed.has(choice.kind)).map(choice => ({
            id: `allow-${choice.kind.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()}`,
            kind: choice.kind,
            allow: true,
          })),
        ],
      });
      router.replace(`/(main)/workspace/${workspaceId}/agents`);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not save the permission policy.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.intro}>Choose what the agent may do without asking. Anything not allowed here is blocked.</Text>
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
        {(loading || busy) && <ActivityIndicator color={agentUpTheme.colors.accent} />}
        {!loading && choices.map(choice => {
          const selected = allowed.has(choice.kind);
          return <Pressable
            key={choice.kind}
            testID={`agent-policy-${choice.kind}`}
            accessibilityRole="checkbox"
            accessibilityState={{ checked: selected, disabled: busy }}
            disabled={busy}
            onPress={() => toggle(choice.kind)}
            style={[styles.choice, selected && styles.selected]}>
            <View style={styles.heading}>
              <Text style={styles.title}>{choice.title}</Text>
              <Text style={selected ? styles.allowed : styles.blocked}>{selected ? 'Allowed' : 'Blocked'}</Text>
            </View>
            <Text style={styles.detail}>{choice.detail}</Text>
          </Pressable>;
        })}
        {!loading && <Pressable
          testID="agent-policy-save"
          accessibilityRole="button"
          disabled={busy}
          onPress={() => void save()}
          style={styles.save}>
          <Text style={styles.saveText}>Save policy</Text>
        </Pressable>}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[4], gap: 12, maxWidth: 672, width: '100%', alignSelf: 'center' },
  intro: auText('muted'),
  choice: { ...auBox('choice'), gap: 4 },
  selected: { borderColor: agentUpTheme.colors.accent },
  heading: { flexDirection: 'row', justifyContent: 'space-between', gap: 12 },
  title: auText('workspaceName'),
  detail: auText('muted'),
  allowed: auText('accent'),
  blocked: auText('muted'),
  save: { ...auBox('button', 'buttonCompact'), alignItems: 'center' },
  saveText: auText('button', 'buttonCompact'),
  error: auText('badgeDanger'),
});
