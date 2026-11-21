import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import {
  AgentLoopControls,
  getAgent,
  getAutonomy,
  getLoop,
  putAutonomy,
  putLoopActivation,
  type AgentLoopStatus,
  type ServerAutonomy,
} from '@agent-up/chat';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { agentUpTheme, auText } from '@agent-up/design-system/native';

/**
 * The unattended-loop checklist with Enable autonomy, Activate, Cancel, and the typed terminal.
 * It lives in the workspace Settings tab, as Settings · Chat holds it in the command center, so
 * Agents and the chat keep their space for the agent and the conversation.
 */
export function AgentLoopSettingsSection({ workspaceId }: { workspaceId: string }) {
  const host = useWorkspaceHost(workspaceId);
  const { expireActiveCredential } = useServers();
  const { server } = useWorkspaces();
  const [autonomy, setAutonomy] = useState<ServerAutonomy | null>(null);
  const [loop, setLoop] = useState<AgentLoopStatus | null | undefined>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    if (!server) return;
    try {
      const session = await getAgent(server, workspaceId);
      setLoop(session?.loop);
      try { setAutonomy(await getAutonomy(server, workspaceId)); }
      catch { setAutonomy(null); }
      setError(null);
    } catch (cause) {
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not load the unattended loop.');
    }
  }, [server, workspaceId, expireActiveCredential]);

  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (!server || !loop?.active) return;
    let cancelled = false;
    const timer = setInterval(() => {
      void getLoop(server, workspaceId)
        .then(next => { if (!cancelled) setLoop(next); })
        .catch(cause => { if (!cancelled) setError(cause instanceof Error ? cause.message : 'Could not load the loop.'); });
    }, 500);
    return () => { cancelled = true; clearInterval(timer); };
  }, [server, workspaceId, loop?.active]);

  const run = async (work: () => Promise<void>, failed: string) => {
    if (!server || busy) return;
    setBusy(true); setError(null);
    try { await work(); }
    catch (cause) { setError(cause instanceof Error ? cause.message : failed); }
    finally { setBusy(false); }
  };

  if (!autonomy || autonomy.preconditions.length === 0) return null;

  return (
    <View style={styles.section} testID="workspace-settings-loop">
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
      {busy && <ActivityIndicator color={agentUpTheme.colors.accent} />}
      <AgentLoopControls
        autonomy={autonomy}
        loop={loop}
        busy={busy}
        onEnable={() => void run(async () => {
          if (!server) return;
          await putAutonomy(server, true);
          setAutonomy(await getAutonomy(server, workspaceId));
        }, 'Could not enable autonomy.')}
        onActivate={() => void run(async () => {
          if (!server) return;
          await putLoopActivation(server, workspaceId, true);
          setLoop(await getLoop(server, workspaceId));
          setAutonomy(await getAutonomy(server, workspaceId));
        }, 'Could not activate the loop.')}
        onCancel={() => void run(async () => {
          if (!server) return;
          await putLoopActivation(server, workspaceId, false);
          setLoop(await getLoop(server, workspaceId));
        }, 'Could not cancel the loop.')}
        onConfigurePolicy={() => host.open({ kind: 'agentPolicy' })}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  section: { gap: 10 },
  error: auText('badgeDanger'),
});
