import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import {
  AgentLoopControls,
  agentRowDetail,
  bindAgent,
  enableAgentCapability,
  getAgent,
  getAutonomy,
  getLoop,
  presentAgentRow,
  putAutonomy,
  putLoopActivation,
  resumeAgent,
  scheduleAgent,
  type AgentId,
  type AgentSession,
  type ServerAutonomy,
} from '@agent-up/chat';
import { boundAgentActionLabel, formatLoopLine } from '@/features/agents/providers/LoopLineProvider';
import { WorkspaceTabBar } from '@/features/shell/components/WorkspaceTabBar';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export function AgentsOverviewScreen({ workspaceId }: { workspaceId: string }) {
  const router = useRouter();
  const { expireActiveCredential } = useServers();
  const { server } = useWorkspaces();
  const [session, setSession] = useState<AgentSession | null>(null);
  const [autonomy, setAutonomy] = useState<ServerAutonomy | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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
      try { setAutonomy(await getAutonomy(server)); }
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

  const runLoop = async (work: () => Promise<void>, failed: string) => {
    if (!server || busy) return;
    setBusy(true); setError(null);
    try { await work(); }
    catch (cause) { setError(cause instanceof Error ? cause.message : failed); }
    finally { setBusy(false); }
  };

  const openChat = () => router.push(`/(main)/workspace/${workspaceId}/agent`);

  const start = async (agent: AgentId) => {
    if (!server || busy) return;
    setBusy(true);
    setError(null);
    try {
      await bindAgent(server, workspaceId, agent);
      const next = await scheduleAgent(server, workspaceId, agent);
      setSession(next);
      openChat();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not start the agent.');
    } finally {
      setBusy(false);
    }
  };

  const enable = async (agent: AgentId) => {
    if (!server || busy) return;
    setBusy(true);
    setError(null);
    try {
      await enableAgentCapability(server, agent);
      setSession(await getAgent(server, workspaceId));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not enable the agent.');
    } finally {
      setBusy(false);
    }
  };

  const resume = async (sessionId: string) => {
    if (!server || busy) return;
    setBusy(true); setError(null);
    try { setSession(await resumeAgent(server, workspaceId, sessionId)); openChat(); }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'Could not resume the session.'); }
    finally { setBusy(false); }
  };

  const agents = session?.agents ?? [];

  return (
    <View style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
        {busy && <ActivityIndicator color={agentUpTheme.colors.accent} />}
        <AgentLoopControls
          autonomy={autonomy}
          loop={session?.loop}
          busy={busy}
          onEnable={() => void runLoop(async () => {
            if (!server) return;
            await putAutonomy(server, true);
            setAutonomy(await getAutonomy(server));
          }, 'Could not enable autonomy.')}
          onActivate={() => void runLoop(async () => {
            if (!server) return;
            await putLoopActivation(server, workspaceId, true);
            const loop = await getLoop(server, workspaceId);
            setSession(current => current ? { ...current, loop } : current);
            setAutonomy(await getAutonomy(server));
          }, 'Could not activate the loop.')}
          onCancel={() => void runLoop(async () => {
            if (!server) return;
            await putLoopActivation(server, workspaceId, false);
            const loop = await getLoop(server, workspaceId);
            setSession(current => current ? { ...current, loop } : current);
          }, 'Could not cancel the loop.')}
        />

        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Workspace sessions</Text>
          {(session?.sessions ?? []).length === 0 && <Text style={styles.detail}>No saved sessions yet.</Text>}
          {(session?.sessions ?? []).map(item => {
            const active = item.sessionId === session?.sessionId && session?.state !== 'stopped';
            return <Pressable key={item.sessionId} testID={`agent-session-${item.sessionId}`} accessibilityRole="button"
              accessibilityLabel={`${active ? 'Open' : 'Resume'} ${item.agent} session`} disabled={busy}
              onPress={() => active ? openChat() : void resume(item.sessionId)} style={styles.current}>
              <View style={styles.sessionHeading}><Text style={styles.tag}>{item.agent}</Text><Text style={styles.branch}>{item.branch}</Text></View>
              <Text style={styles.currentTitle}>{item.description}</Text>
              <Text style={styles.action}>{active ? `Current · ${session?.state}` : 'Resume session →'}</Text>
            </Pressable>;
          })}
        </View>

        {session?.boundAgent ? (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Bound agent</Text>
            <Pressable
              testID={`agent-bound-${session.boundAgent}`}
              accessibilityRole="button"
              accessibilityLabel={boundAgentActionLabel(session.boundAgent, session.agent, agents) ?? `Open ${session.boundAgent}`}
              disabled={busy}
              onPress={() => {
                const bound = session.boundAgent;
                if (!bound) return;
                session.agent === bound ? openChat() : void start(bound);
              }}
              style={styles.card}>
              <Text style={styles.title}>{session.boundAgent}</Text>
              <Text style={styles.detail}>{formatLoopLine(session.loop) ?? (session.agent ? 'Open existing session' : 'Start the bound agent')}</Text>
            </Pressable>
          </View>
        ) : (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>{session?.agent ? 'Start another session' : 'Start a session'}</Text>
          {agents.length === 0 && !error && <Text style={styles.detail}>Loading available agents…</Text>}
          {agents.map(agent => {
            const currentAgent = agent.agent === session?.agent;
            const row = presentAgentRow(agent);
            if (currentAgent || row.canSelect) {
              return (
                <Pressable
                  key={agent.agent}
                  testID={`agent-start-${agent.agent}`}
                  accessibilityRole="button"
                  accessibilityLabel={currentAgent ? `Open ${agent.displayName}` : `Start ${agent.displayName}`}
                  disabled={busy}
                  onPress={() => currentAgent ? openChat() : void start(agent.agent)}
                  style={styles.card}>
                  <Text style={styles.title}>{agent.displayName}</Text>
                  <Text style={styles.detail}>
                    {currentAgent ? 'Open existing session' : agentRowDetail(agent)}
                  </Text>
                </Pressable>
              );
            }
            return (
              <View key={agent.agent} style={[styles.card, row.isBlocked && styles.disabled]}>
                <Text style={styles.title}>{agent.displayName}</Text>
                <Text style={styles.detail}>{row.status}</Text>
                {row.detail ? <Text style={styles.detail}>{row.detail}</Text> : null}
                {row.remedy ? <Text style={styles.detail}>{row.remedy}</Text> : null}
                {row.canEnable ? (
                  <Pressable
                    testID={`agent-enable-${agent.agent}`}
                    accessibilityRole="button"
                    accessibilityLabel={`Enable ${agent.displayName}`}
                    disabled={busy}
                    onPress={() => void enable(agent.agent)}>
                    <Text style={styles.action}>Enable</Text>
                  </Pressable>
                ) : null}
              </View>
            );
          })}
        </View>
        )}
      </ScrollView>
      <WorkspaceTabBar workspaceId={workspaceId} active="agents" />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8], gap: 16, maxWidth: 672, width: '100%', alignSelf: 'center' },
  current: { ...auBox('card'), gap: agentUpTheme.spacing[2] },
  currentTitle: auText('pageTitle'),
  action: auText('accent'),
  section: { gap: 10 },
  sectionTitle: auText('fieldLabel'),
  card: { ...auBox('choice'), gap: 2 },
  title: auText('workspaceName'),
  detail: auText('muted'),
  error: auText('badgeDanger'),
  disabled: { opacity: 0.38 },
  sessionHeading: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  tag: { ...auText('badge'), color: agentUpTheme.colors.accent },
  branch: auText('muted'),
});
