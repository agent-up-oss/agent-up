import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { CapabilityModule } from '../models/CapabilityModule';
import { disableCapabilityModule, enableCapabilityModule, listCapabilityModules } from '../providers/CapabilityModulesApiProvider';

export function CapabilityModulesSection({ onReload }: { onReload?: (reload: () => void) => void }) {
  const { activeServer, expireActiveCredential } = useServers();
  const [modules, setModules] = useState<CapabilityModule[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    if (!activeServer) return;
    try {
      setModules(await listCapabilityModules(activeServer));
      setError(null);
    } catch (cause) {
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not load capability modules.');
    } finally {
      setLoading(false);
    }
  }, [activeServer, expireActiveCredential]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => { onReload?.(() => { void load(); }); }, [load, onReload]);

  const toggle = async (module: CapabilityModule) => {
    if (!activeServer || busyId) return;
    setBusyId(module.id);
    setError(null);
    try {
      if (module.enabled) await disableCapabilityModule(activeServer, module.id);
      else await enableCapabilityModule(activeServer, module.id, module.version);
      await load();
    } catch (cause) {
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not update the capability module.');
    } finally {
      setBusyId(null);
    }
  };

  return (
    <View style={styles.section}>
      <Text style={styles.heading}>Capabilities</Text>
      <Text style={styles.intro}>Enable packages on this Server. Mobile never talks to the remote registry.</Text>
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
      {loading && <ActivityIndicator color={agentUpTheme.colors.accent} />}
      {!loading && modules.length === 0 && !error &&
        <Text style={styles.intro}>No capability modules in the Server registry.</Text>}
      {modules.map(module => (
        <View key={`${module.id}:${module.version}`} style={styles.card}>
          <View style={styles.header}>
            <Text style={styles.title}>{module.displayName}</Text>
            <Text style={styles.meta}>{module.canRun ? 'Ready' : module.state}</Text>
          </View>
          {module.messages.length > 0 &&
            <Text style={styles.detail}>{module.messages.join(' ')}</Text>}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={module.enabled ? `Disable ${module.displayName}` : `Enable ${module.displayName}`}
            disabled={busyId === module.id}
            onPress={() => { void toggle(module); }}
            style={styles.button}>
            <Text style={styles.buttonText}>{module.enabled ? 'Disable' : 'Enable'}</Text>
          </Pressable>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  section: { gap: agentUpTheme.spacing[3] },
  heading: auText('fieldLabel'),
  intro: auText('muted'),
  error: auText('badgeDanger'),
  card: { ...auBox('workspace'), gap: agentUpTheme.spacing[2] },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: agentUpTheme.spacing[2] },
  title: auText('workspaceName'),
  meta: auText('workspaceBranch'),
  detail: auText('muted'),
  button: { ...auBox('button', 'buttonSecondary'), alignItems: 'center', justifyContent: 'center' },
  buttonText: auText('buttonSecondary'),
});
