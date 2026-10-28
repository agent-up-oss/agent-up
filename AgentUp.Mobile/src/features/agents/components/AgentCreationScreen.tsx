import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { getAgent, scheduleAgent, unbindAgent, type AgentDescriptor } from '@agent-up/chat';
import { useWorkspaceInnerShell } from '@/features/shell/hooks/useWorkspaceInnerShell';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';

export function AgentCreationScreen({ workspaceId }: { workspaceId: string }) {
  const host = useWorkspaceHost(workspaceId);
  const { server } = useWorkspaces();
  const { expireActiveCredential } = useServers();
  const [agents, setAgents] = useState<AgentDescriptor[]>([]);
  const [staleBinding, setStaleBinding] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inner = useWorkspaceInnerShell(workspaceId, 'Create agent', AGENTS);
  const shellConfig = useMemo(() => inner, [inner]);
  useShellConfig(shellConfig);

  const load = useCallback(async () => {
    if (!server) return;
    setLoading(true);
    try {
      const session = await getAgent(server, workspaceId);
      if (!session) throw new Error('Could not load enabled agents.');
      setAgents(session.agents.filter(agent => agent.unavailability !== 'capabilityNotEnabled'));
      setStaleBinding(Boolean(session.boundAgent && !session.agent));
      setError(null);
    } catch (cause) {
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not load enabled agents.');
    } finally {
      setLoading(false);
    }
  }, [expireActiveCredential, server, workspaceId]);

  useEffect(() => { void load(); }, [load]);

  const create = async (agent: AgentDescriptor) => {
    if (!server || busy || !agent.available) return;
    setBusy(true);
    setError(null);
    try {
      if (staleBinding) await unbindAgent(server, workspaceId);
      await scheduleAgent(server, workspaceId, agent.agent);
      host.open({ kind: 'agent' }, { replace: true });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not create the agent.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
        {(loading || busy) && <ActivityIndicator color={agentUpTheme.colors.accent} />}
        {!loading && agents.length === 0 && <Text style={styles.detail}>No agent capabilities are enabled. Enable one in Settings first.</Text>}
        {agents.map(agent => {
          return (
            <Pressable
              key={agent.agent}
              testID={`agent-create-${agent.agent}`}
              accessibilityRole="button"
              accessibilityLabel={`Create ${agent.displayName} agent`}
              accessibilityState={{ disabled: busy || !agent.available }}
              disabled={busy || !agent.available}
              onPress={() => void create(agent)}
              style={[styles.card, !agent.available && styles.disabled]}>
              <Text style={styles.title}>{agent.displayName}</Text>
              <Text style={agent.available ? styles.ready : styles.unavailable}>
                {agent.available ? 'Ready' : 'Disabled'}
              </Text>
            </Pressable>
          );
        })}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8], gap: 12, maxWidth: 672, width: '100%', alignSelf: 'center' },
  card: { ...auBox('choice'), gap: 2 },
  title: auText('workspaceName'),
  detail: auText('muted'),
  ready: auText('accent'),
  unavailable: auText('muted'),
  error: auText('badgeDanger'),
  disabled: { opacity: 0.5 },
});

const AGENTS = { kind: 'agents' } as const;
