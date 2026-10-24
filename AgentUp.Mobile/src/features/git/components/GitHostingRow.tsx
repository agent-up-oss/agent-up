import { useEffect, useRef, useState } from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { workspaceScopeKey } from '@/features/workspaces/providers/WorkspaceScopeKeyProvider';
import { agentUpTheme, auText } from '@agent-up/design-system/native';
import type { ForgeHosting } from '../models/ForgeHosting';
import { getForgeHosting, startForgeLogin } from '../providers/GitApiProvider';
import { hostingCopy } from '../providers/GitHostingCopyProvider';
import { signInUrlAfterScopeChange, signInUrlForCurrentScope } from '../providers/GitHostingSignInProvider';

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
  const scopeRef = useRef(scopeKey);
  const [hosting, setHosting] = useState<ForgeHosting | null>(null);
  const [signInUrl, setSignInUrl] = useState<string | null>(null);
  const [signInError, setSignInError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setSignInUrl(current => signInUrlAfterScopeChange(scopeRef.current, scopeKey, current));
    setSignInError(null);
    scopeRef.current = scopeKey;
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
        remedy: 'Check the repository host URL and retry.',
      });
    });
    return () => {
      cancelled = true;
    };
  }, [server, workspaceId, scopeKey, reloadNonce]);

  const copy = hostingCopy(hosting);
  if (!copy.visible) return null;

  const checkColor = copy.checksTone === 'danger'
    ? agentUpTheme.colors.statusDanger
    : copy.checksTone === 'warning'
      ? agentUpTheme.colors.statusWarning
      : agentUpTheme.colors.statusHealthy;

  return (
    <View style={styles.row} testID="git-hosting">
      <Text style={styles.title} testID="git-hosting-title">{copy.title}</Text>
      {copy.remedy ? (
        <Text style={styles.muted} testID="git-hosting-remedy">{copy.remedy}</Text>
      ) : null}
      {copy.canSignIn && server ? (
        <Pressable
          testID="git-hosting-sign-in"
          accessibilityRole="button"
          accessibilityLabel="Connect repository host"
          onPress={() => {
            const startedScope = scopeKey;
            setSignInError(null);
            void startForgeLogin(server, workspaceId).then(started => {
              const url = signInUrlForCurrentScope(startedScope, scopeRef.current, started.authorizationUrl);
              if (!url) return;
              setSignInUrl(url);
              return Linking.openURL(url);
            }).catch((error: unknown) => {
              setSignInError(error instanceof Error ? error.message : 'Could not start repository host setup.');
            });
          }}
        >
          <Text style={styles.link}>Connect</Text>
        </Pressable>
      ) : null}
      {signInUrl ? (
        <Pressable
          testID="git-hosting-open-sign-in"
          accessibilityRole="button"
          accessibilityLabel="Continue repository host setup"
          onPress={() => { void Linking.openURL(signInUrl); }}>
          <Text style={styles.link}>Continue setup</Text>
        </Pressable>
      ) : null}
      {signInError ? <Text style={styles.error}>{signInError}</Text> : null}
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
            <Pressable
              key={thread.id}
              accessibilityRole="button"
              accessibilityLabel={thread.summary || thread.id}
              onPress={() => { void Linking.openURL(thread.url); }}>
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
  error: { ...auText('fieldLabel'), color: agentUpTheme.colors.statusDanger },
  threads: { gap: 4 },
  link: { ...auText('fieldLabel'), color: agentUpTheme.colors.accent },
});
