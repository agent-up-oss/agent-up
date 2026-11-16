import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import {
  AgentLoopControls,
  getAgent,
  getAutonomy,
  getLoop,
  putAutonomy,
  putLoopActivation,
  type AgentSession,
  type ServerAutonomy,
} from '@agent-up/chat';
import { boundAgentActionLabel, formatLoopLine } from '@/features/agents/providers/LoopLineProvider';
import { WorkspaceTabs } from '@/features/shell/components/WorkspaceTabs';
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
  const [busy, setBusy] = useState(false);
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

  const runLoop = async (work: () => Promise<void>, failed: string) => {
    if (!server || busy) return;
    setBusy(true); setError(null);
    try { await work(); }
    catch (cause) { setError(cause instanceof Error ? cause.message : failed); }
    finally { setBusy(false); }
  };

  const openChat = () => host.open({ kind: 'agent' });

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
            setAutonomy(await getAutonomy(server, workspaceId));
          }, 'Could not enable autonomy.')}
          onActivate={() => void runLoop(async () => {
            if (!server) return;
            await putLoopActivation(server, workspaceId, true);
            const loop = await getLoop(server, workspaceId);
            setSession(current => current ? { ...current, loop } : current);
            setAutonomy(await getAutonomy(server, workspaceId));
          }, 'Could not activate the loop.')}
          onCancel={() => void runLoop(async () => {
            if (!server) return;
            await putLoopActivation(server, workspaceId, false);
            const loop = await getLoop(server, workspaceId);
            setSession(current => current ? { ...current, loop } : current);
          }, 'Could not cancel the loop.')}
          onConfigurePolicy={() => host.open({ kind: 'agentPolicy' })}
        />

        {session?.boundAgent && hasCreatedBoundAgent ? (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Bound agent</Text>
            <Pressable
              testID={`agent-bound-${session.boundAgent}`}
              accessibilityRole="button"
              accessibilityLabel={boundAgentActionLabel(session.boundAgent, session.agent, agents) ?? `Open ${session.boundAgent}`}
              disabled={busy}
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
            disabled={busy}
            onPress={() => host.open({ kind: 'agentCreate' })}
            style={styles.createButton}>
            <Text style={styles.createIcon}>+</Text>
          </Pressable>
        )}
      </ScrollView>
      <WorkspaceTabs workspaceId={workspaceId} active="agents" />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8], gap: 16, maxWidth: 672, width: '100%', alignSelf: 'center' },
  section: { gap: 10 },
  sectionTitle: auText('fieldLabel'),
  card: { ...auBox('choice'), gap: 2 },
  createButton: { ...auBox('titleTool'), alignItems: 'center', justifyContent: 'center' },
  createIcon: { ...auText('accent'), fontSize: 20, fontWeight: '700', lineHeight: 22 },
  title: auText('workspaceName'),
  detail: auText('muted'),
  error: auText('badgeDanger'),
});
