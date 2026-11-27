import { useCallback, useEffect, useMemo, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import {
  getAgent,
  getAutonomy,
  getLoop,
  type AgentSession,
  type ServerAutonomy,
} from '@agent-up/chat';
import { boundAgentActionLabel, formatLoopLine, loopSettingsSummary } from '@/features/agents/providers/LoopLineProvider';
import { WorkspacePage } from '@/features/shell/components/WorkspacePage';
import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export function AgentsOverviewScreen({ workspaceId }: { workspaceId: string }) {
  const host = useWorkspaceHost(workspaceId);
  const { expireActiveCredential } = useServers();
  const { server } = useWorkspaces();
  const [session, setSession] = useState<AgentSession | null>(null);
  const [autonomy, setAutonomy] = useState<ServerAutonomy | null>(null);
  const [error, setError] = useState<string | null>(null);
  const loopSummary = loopSettingsSummary(autonomy);
  const agents = session?.agents ?? [];
  const boundAgent = agents.find(item => item.agent.toLowerCase() === session?.boundAgent?.toLowerCase());
  const hasCreatedBoundAgent = Boolean(boundAgent && session?.agent
    && boundAgent.agent.toLowerCase() === session.agent.toLowerCase());

  const shellConfig = useMemo(() => ({
    title: 'Agents',
    rightAction: null,
    sidebarContent: null,
  }), []);
  useShellConfig(shellConfig);

  const load = useCallback(async () => {
    if (!server) return;
    try {
      setSession(await getAgent(server, workspaceId));
      try { setAutonomy(await getAutonomy(server, workspaceId)); }
      catch { setAutonomy(null); }
      setError(null);
    } catch (cause) {
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not load agents.');
    }
  }, [server, workspaceId, expireActiveCredential]);

  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (!server || !session?.loop?.active) return;
    let cancelled = false;
    const timer = setInterval(() => {
      void getLoop(server, workspaceId)
        .then(loop => { if (!cancelled) setSession(current => current ? { ...current, loop } : current); })
        .catch(cause => { if (!cancelled) setError(cause instanceof Error ? cause.message : 'Could not load the loop.'); });
    }, 500);
    return () => { cancelled = true; clearInterval(timer); };
  }, [server, workspaceId, session?.loop?.active]);

  const openChat = () => host.open({ kind: 'agent' });

  return (
    <WorkspacePage workspaceId={workspaceId} active="agents">
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
        {loopSummary && (
          <Pressable
            testID="agent-loop-summary"
            accessibilityRole="button"
            accessibilityLabel={`${loopSummary}. Open Settings`}
            onPress={() => host.open({ kind: 'settings' })}
            style={styles.summary}>
            <Text style={styles.detail}>{loopSummary}</Text>
            <Text style={styles.summaryAction}>Settings</Text>
          </Pressable>
        )}

        {session?.boundAgent && hasCreatedBoundAgent ? (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Bound agent</Text>
            <Pressable
              testID={`agent-bound-${session.boundAgent}`}
              accessibilityRole="button"
              accessibilityLabel={boundAgentActionLabel(session.boundAgent, session.agent, agents) ?? `Open ${session.boundAgent}`}
              onPress={openChat}
              style={styles.card}>
              <Text style={styles.title}>{boundAgent?.displayName ?? session.boundAgent}</Text>
              <Text style={styles.detail}>{formatLoopLine(session.loop) ?? (session.agent ? 'Open existing session' : 'Start the bound agent')}</Text>
            </Pressable>
          </View>
        ) : (
          session && <Pressable
            testID="agent-create"
            accessibilityRole="button"
            accessibilityLabel="Create bound agent"
            onPress={() => host.open({ kind: 'agentCreate' })}
            style={styles.createButton}>
            <Text style={styles.createIcon}>+</Text>
          </Pressable>
        )}
      </ScrollView>
    </WorkspacePage>
  );
}

const styles = StyleSheet.create({
  content: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8], gap: 16, width: '100%' },
  section: { gap: 10 },
  sectionTitle: auText('fieldLabel'),
  card: { ...auBox('choice'), gap: 2 },
  summary: { ...auBox('choice'), flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  summaryAction: auText('accent'),
  createButton: { ...auBox('titleTool'), alignItems: 'center', justifyContent: 'center' },
  createIcon: { ...auText('accent'), fontSize: 20, fontWeight: '700', lineHeight: 22 },
  title: auText('workspaceName'),
  detail: auText('muted'),
  error: auText('badgeDanger'),
});
