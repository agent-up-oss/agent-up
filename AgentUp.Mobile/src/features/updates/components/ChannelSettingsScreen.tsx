import { Picker } from '@react-native-picker/picker';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import type { ChannelRelease } from '../models/ChannelRelease';
import { getChannelReleases } from '../providers/GitHubChannelReleaseProvider';
import { getInstalledRelease, installRelease, isUpgrade } from '../providers/WebReleaseInstaller';

export function ChannelSettingsScreen() {
  const [releases, setReleases] = useState<ChannelRelease[]>([]);
  const installed = getInstalledRelease();
  const isSourceBuild = installed.channel === 'development' && installed.sha === 'source';
  const [selected, setSelected] = useState(isSourceBuild ? '' : installed.channel);
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(true);
  const channels = useMemo(() => Array.from(new Set(releases.map(release => release.channel))), [releases]);
  const latest = useMemo(() => releases.find(release => release.channel === selected), [releases, selected]);
  const canInstall = !busy && !!latest && isUpgrade(installed, latest);
  const actionLabel = latest
    ? isSourceBuild
      ? `Install rc-${latest.channel}-${latest.sha}`
      : selected === installed.channel
        ? `Update to rc-${latest.channel}-${latest.sha}`
        : `Switch to rc-${latest.channel}-${latest.sha}`
    : 'Select a channel';

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
          {isSourceBuild ? 'Running from source (not an installed release)' : `Installed: rc-${installed.channel}-${installed.sha}`}
        </Text>
        <Text style={styles.label}>Channel</Text>
        <View style={styles.pickerContainer}>
          <Picker
            accessibilityLabel="Release channel"
            enabled={!busy && channels.length > 0}
            selectedValue={selected}
            onValueChange={value => setSelected(String(value))}
            dropdownIconColor="#2bf27a"
            {...(Platform.OS === 'android' ? { dropdownIconRippleColor: 'rgba(43, 242, 122, 0.16)' } : {})}
            mode="dropdown"
            style={styles.picker}
            itemStyle={styles.pickerItem}
          >
            <Picker.Item label={channels.length === 0 ? 'No channels available' : 'Select a channel'} value="" color="#aebcb3" />
            {channels.map(channel => <Picker.Item key={channel} label={channel} value={channel} color="#f5fbf7" />)}
          </Picker>
        </View>
        {busy && <ActivityIndicator color="#00d66b" />}
        {!busy && releases.length === 0 && <Text style={styles.meta}>No published release channels found yet.</Text>}
        {latest && <Text style={styles.meta}>Available: rc-{latest.channel}-{latest.sha}</Text>}
        <Pressable accessibilityRole="button" disabled={!canInstall}
          onPress={update} style={[styles.button, !canInstall && styles.disabled]}>
          <Text style={styles.buttonText}>{actionLabel}</Text>
        </Pressable>
        {!!status && <Text accessibilityRole="alert" style={styles.status}>{status}</Text>}
        <Pressable accessibilityRole="button" disabled={busy} onPress={refresh}><Text style={styles.link}>Check for updates</Text></Pressable>
      </View>
    </ScrollView></SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#000000' }, content: { padding: 20, paddingTop: 78, paddingBottom: 32, gap: 20 },
  title: { color: '#f5fbf7', fontSize: 36, lineHeight: 40, fontWeight: '800' },
  card: { padding: 20, borderRadius: 8, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505', gap: 16 },
  heading: { color: '#f5fbf7', fontSize: 20, fontWeight: '700' }, meta: { color: '#aebcb3', lineHeight: 21 },
  label: { color: '#f5fbf7', fontWeight: '700' },
  pickerContainer: { borderRadius: 8, overflow: 'hidden' },
  picker: { width: '100%', height: 50, color: '#f5fbf7', backgroundColor: '#080808',
    borderColor: '#287038', borderWidth: 1, borderRadius: 8, paddingHorizontal: 12 },
  pickerItem: { color: '#f5fbf7', backgroundColor: '#080808' },
  status: { color: '#2bf27a' }, button: { minHeight: 48, alignItems: 'center', justifyContent: 'center', borderRadius: 8,
    paddingHorizontal: 16, backgroundColor: '#00d66b' },
  disabled: { opacity: 0.38 }, buttonText: { color: '#000000', fontWeight: '800' },
  link: { color: '#00d66b', fontWeight: '700', textAlign: 'center' },
});
