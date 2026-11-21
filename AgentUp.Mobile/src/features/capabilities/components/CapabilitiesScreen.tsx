import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { CapabilityModule } from '../models/CapabilityModule';
import {
  disableCapabilityModule,
  enableCapabilityModule,
  listCapabilityModules,
  updateCapabilitySettings,
} from '../providers/CapabilityModulesApiProvider';
import {
  capabilityButton,
  capabilityStatus,
  changedSettings,
  initialValue,
  settingLabel,
  settingPlaceholder,
  showsSettings,
} from '../providers/CapabilityPresentationProvider';

export function CapabilityModulesSection({ onReload }: { onReload?: (reload: () => void) => void }) {
  const { activeServer, expireActiveCredential } = useServers();
  const [modules, setModules] = useState<CapabilityModule[]>([]);
  const [drafts, setDrafts] = useState<Record<string, Record<string, string>>>({});
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    if (!activeServer) return;
    try {
      setModules(await listCapabilityModules(activeServer));
      setDrafts({});
      setError(null);
    } catch (cause) {
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not load capabilities.');
    } finally {
      setLoading(false);
    }
  }, [activeServer, expireActiveCredential]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => { onReload?.(() => { void load(); }); }, [load, onReload]);

  const change = async (module: CapabilityModule, action: () => Promise<unknown>) => {
    if (!activeServer || busyId) return;
    setBusyId(module.id);
    setError(null);
    try {
      await action();
      await load();
    } catch (cause) {
      if (isUnauthorized(cause)) {
        expireActiveCredential();
        return;
      }
      setError(cause instanceof Error ? cause.message : 'Could not update the capability.');
    } finally {
      setBusyId(null);
    }
  };

  const toggle = (module: CapabilityModule) => change(module, () => module.enabled
    ? disableCapabilityModule(activeServer!, module.id)
    : enableCapabilityModule(activeServer!, module.id));

  const save = (module: CapabilityModule) => {
    const values = changedSettings(module.settings ?? [], drafts[module.id] ?? {});
    if (Object.keys(values).length === 0) return;
    void change(module, () => updateCapabilitySettings(activeServer!, module.id, values));
  };

  const edit = (id: string, key: string, value: string) =>
    setDrafts(current => ({ ...current, [id]: { ...current[id], [key]: value } }));

  return (
    <View style={styles.section}>
      <Text style={styles.heading}>Capabilities</Text>
      <Text style={styles.intro}>Built into this Server. Enable the ones whose software it can find.</Text>
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
      {loading && <ActivityIndicator color={agentUpTheme.colors.accent} />}
      {!loading && modules.length === 0 && !error &&
        <Text style={styles.intro}>The Server reports no capabilities.</Text>}
      {modules.map(module => {
        const button = capabilityButton(module);
        return (
          <View key={module.id} style={styles.card}>
            <View style={styles.header}>
              <View style={styles.identity}>
                <Text style={styles.title}>{module.displayName}</Text>
                <Text style={styles.meta}>{capabilityStatus(module)}</Text>
              </View>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`${button.label} ${module.displayName}`}
                accessibilityState={{ disabled: button.disabled || busyId === module.id }}
                disabled={button.disabled || busyId === module.id}
                onPress={() => { void toggle(module); }}
                style={[styles.button, button.disabled && styles.buttonUnavailable]}>
                <Text style={styles.buttonText}>{button.label}</Text>
              </Pressable>
            </View>
            {showsSettings(module) &&
              <View style={styles.settings}>
                {(module.settings ?? []).map(setting => (
                  <View key={setting.key} style={styles.field}>
                    <Text style={styles.label}>{settingLabel(setting)}</Text>
                    <TextInput
                      accessibilityLabel={`${module.displayName} ${setting.label}`}
                      autoCapitalize="none"
                      autoCorrect={false}
                      secureTextEntry={setting.kind === 'secret'}
                      keyboardType={setting.kind === 'url' ? 'url' : 'default'}
                      placeholder={settingPlaceholder(setting)}
                      placeholderTextColor={agentUpTheme.colors.textFaint}
                      value={drafts[module.id]?.[setting.key] ?? initialValue(setting)}
                      onChangeText={value => edit(module.id, setting.key, value)}
                      style={styles.input}
                    />
                  </View>
                ))}
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={`Save ${module.displayName} settings`}
                  disabled={busyId === module.id}
                  onPress={() => save(module)}
                  style={styles.save}>
                  <Text style={styles.saveText}>Save</Text>
                </Pressable>
              </View>}
          </View>
        );
      })}
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
  identity: { flex: 1, gap: agentUpTheme.spacing[1] },
  title: auText('workspaceName'),
  meta: auText('workspaceBranch'),
  button: { ...auBox('button', 'buttonSecondary'), alignItems: 'center', justifyContent: 'center' },
  buttonUnavailable: { opacity: 0.6 },
  buttonText: auText('buttonSecondary'),
  settings: { gap: agentUpTheme.spacing[2] },
  field: { gap: agentUpTheme.spacing[1] },
  label: auText('fieldLabel'),
  input: { ...auBox('input'), ...auText('input') },
  save: { ...auBox('button'), alignItems: 'center', justifyContent: 'center', alignSelf: 'flex-end' },
  saveText: auText('button'),
});
