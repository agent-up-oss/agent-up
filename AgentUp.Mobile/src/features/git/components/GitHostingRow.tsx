import { useEffect, useState } from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { workspaceScopeKey } from '@/features/workspaces/providers/WorkspaceScopeKeyProvider';
import { agentUpTheme, auText } from '@agent-up/design-system/native';
import type { ForgeHosting } from '../models/ForgeHosting';
import { getForgeHosting } from '../providers/GitApiProvider';
import { hostingCopy } from '../providers/GitHostingCopyProvider';

export function GitHostingRow({
  workspaceId,
  detail = false,
  reloadNonce = 0,
}: {
  workspaceId: string;
  detail?: boolean;
  reloadNonce?: number;
}) {
  const { activeServer } = useServers();
  const { server } = useWorkspaces();
  const scopeKey = workspaceScopeKey(activeServer?.id ?? '', workspaceId);
  const [hosting, setHosting] = useState<ForgeHosting | null>(null);

  useEffect(() => {
    let cancelled = false;
    if (!server) {
      setHosting(null);
      return;
    }
    void getForgeHosting(server, workspaceId).then(next => {
      if (!cancelled) setHosting(next);
    }).catch(() => {
      if (!cancelled) setHosting({
        availability: 'unreachable',
        moduleId: null,
        proposal: null,
        checks: [],
        threads: [],
        mergeability: null,
        detail: 'Could not reach the host.',
      });
    });
    return () => {
      cancelled = true;
    };
  }, [server, workspaceId, scopeKey, reloadNonce]);

  const copy = hostingCopy(hosting);
  if (!copy.visible) return null;

  const checkColor = copy.checks === 'Checks failed'
    ? agentUpTheme.colors.statusDanger
    : copy.checks === 'Checks pending'
      ? agentUpTheme.colors.statusWarning
      : agentUpTheme.colors.statusHealthy;

  return (
    <View style={styles.row} testID="git-hosting">
      <Text style={styles.title}>{copy.title}</Text>
      {copy.checks ? (
        <View style={styles.facts}>
          <Text style={[styles.fact, { color: checkColor }]}>{copy.checks}</Text>
          <Text style={styles.muted}>{copy.threads}</Text>
          <Text style={styles.muted}>{copy.mergeability}</Text>
        </View>
      ) : null}
      {detail && hosting?.threads.length ? (
        <View style={styles.threads}>
          {hosting.threads.map(thread => (
            <Pressable key={thread.id} onPress={() => { void Linking.openURL(thread.url); }}>
              <Text style={styles.link}>{thread.summary || thread.id}</Text>
            </Pressable>
          ))}
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    gap: 6,
    padding: agentUpTheme.spacing[3],
    backgroundColor: agentUpTheme.colors.surfaceRaised,
    borderRadius: agentUpTheme.radii.md,
  },
  title: auText('fieldLabel'),
  facts: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  fact: auText('fieldLabel'),
  muted: { ...auText('fieldLabel'), color: agentUpTheme.colors.textMuted },
  threads: { gap: 4 },
  link: { ...auText('fieldLabel'), color: agentUpTheme.colors.accent },
});
