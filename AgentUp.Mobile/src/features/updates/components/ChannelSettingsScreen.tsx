import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, SafeAreaView, ScrollView, StyleSheet, Text, View } from 'react-native';
import type { ChannelRelease } from '../models/ChannelRelease';
import { getChannelReleases } from '../providers/GitHubChannelReleaseProvider';
import { getInstalledRelease, installRelease, isUpgrade } from '../providers/WebReleaseInstaller';

export function ChannelSettingsScreen() {
  const [releases, setReleases] = useState<ChannelRelease[]>([]);
  const installed = getInstalledRelease();
  const isSourceBuild = installed.channel === 'development' && installed.sha === 'source';
  const [selected, setSelected] = useState(isSourceBuild ? '' : installed.channel);
  const [channelMenuOpen, setChannelMenuOpen] = useState(false);
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(true);
  const channels = useMemo(() => Array.from(new Set(releases.map(release => release.channel))), [releases]);
  const latest = useMemo(() => releases.find(release => release.channel === selected), [releases, selected]);

  const refresh = async () => {
    setBusy(true); setStatus('');
    try {
      const available = await getChannelReleases();
      setReleases(available);
      const availableChannels = new Set(available.map(release => release.channel));
      setSelected(current => availableChannels.has(current) ? current : '');
    }
    catch (error) { setStatus(error instanceof Error ? error.message : 'Could not load channels.'); }
    finally { setBusy(false); }
  };
  useEffect(() => { void refresh(); }, []);

  const update = async () => {
    if (!latest) return;
    setBusy(true); setStatus('Downloading the complete release…');
    try { await installRelease(latest); }
    catch (error) { setStatus(error instanceof Error ? error.message : 'Update failed.'); setBusy(false); }
  };

  return (
    <SafeAreaView style={styles.screen}><ScrollView contentContainerStyle={styles.content}>
      <Text accessibilityRole="header" style={styles.title}>Settings</Text>
      <View style={styles.card}>
        <Text style={styles.heading}>Release channel</Text>
        <Text style={styles.meta}>
          {isSourceBuild ? 'Running from source (not an installed release)' : `Installed: ${installed.channel} @ ${installed.sha.slice(0, 7)}`}
        </Text>
        <Text style={styles.label}>Channel</Text>
        <Pressable accessibilityRole="button" accessibilityState={{ expanded: channelMenuOpen }}
          onPress={() => setChannelMenuOpen(open => !open)} style={styles.select}>
          <Text style={[styles.selectText, !selected && styles.placeholder]}>{selected || 'Select a channel'}</Text>
          <Text style={styles.chevron}>{channelMenuOpen ? '▲' : '▼'}</Text>
        </Pressable>
        {channelMenuOpen && <View style={styles.menu}>
          {channels.length === 0
            ? <Text style={styles.emptyOption}>No channels available</Text>
            : channels.map(channel => (
              <Pressable key={channel} accessibilityRole="button" onPress={() => { setSelected(channel); setChannelMenuOpen(false); }}
                style={[styles.option, selected === channel && styles.selectedOption]}>
                <Text style={styles.channelText}>{channel}</Text>
              </Pressable>
            ))}
        </View>}
        {busy && <ActivityIndicator color="#60a5fa" />}
        {!busy && releases.length === 0 && <Text style={styles.meta}>No published release channels found yet.</Text>}
        {latest && <Text style={styles.meta}>Available: {latest.channel} @ {latest.sha.slice(0, 7)}</Text>}
        {!!status && <Text accessibilityRole="alert" style={styles.status}>{status}</Text>}
        <Pressable accessibilityRole="button" disabled={busy || !latest || !isUpgrade(installed, latest)}
          onPress={update} style={[styles.button, (busy || !latest || !isUpgrade(installed, latest)) && styles.disabled]}>
          <Text style={styles.buttonText}>{!isSourceBuild && selected === installed.channel ? 'Update' : 'Switch channel'}</Text>
        </Pressable>
        <Pressable accessibilityRole="button" disabled={busy} onPress={refresh}><Text style={styles.link}>Check for updates</Text></Pressable>
      </View>
    </ScrollView></SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#111827' }, content: { padding: 24, gap: 20 },
  title: { color: '#f9fafb', fontSize: 32, fontWeight: '700' },
  card: { padding: 24, borderRadius: 20, backgroundColor: '#1f2937', gap: 16 },
  heading: { color: '#f9fafb', fontSize: 20, fontWeight: '600' }, meta: { color: '#d1d5db' },
  label: { color: '#d1d5db', fontWeight: '600' },
  select: { minHeight: 48, paddingHorizontal: 14, borderColor: '#4b5563', borderWidth: 1, borderRadius: 10,
    flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', backgroundColor: '#111827' },
  selectText: { color: '#f9fafb', fontSize: 16 }, placeholder: { color: '#9ca3af' }, chevron: { color: '#9ca3af' },
  menu: { marginTop: -12, borderColor: '#4b5563', borderWidth: 1, borderRadius: 10, overflow: 'hidden', backgroundColor: '#111827' },
  option: { paddingHorizontal: 14, paddingVertical: 13 }, selectedOption: { backgroundColor: '#1e3a5f' },
  emptyOption: { color: '#9ca3af', paddingHorizontal: 14, paddingVertical: 13 }, channelText: { color: '#f9fafb' },
  status: { color: '#fbbf24' }, button: { alignItems: 'center', borderRadius: 10, padding: 14, backgroundColor: '#2563eb' },
  disabled: { opacity: 0.4 }, buttonText: { color: '#fff', fontWeight: '700' }, link: { color: '#60a5fa', textAlign: 'center' },
});
