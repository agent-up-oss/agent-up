import { useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useServers } from '../controllers/ServersContext';
import { normalizeServerUrl, probeServer } from '../providers/ServerUrlProvider';

export function ServerSetupScreen() {
  const { activeServer, saveServer } = useServers();
  const [url, setUrl] = useState('');
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(false);

  const tryAndSave = async () => {
    setBusy(true); setStatus('Trying server…');
    try {
      const normalized = normalizeServerUrl(url);
      await probeServer(normalized);
      saveServer(normalized); setUrl(''); setStatus(`Connected to ${normalized}`);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Could not connect to the server.');
    } finally { setBusy(false); }
  };

  return <SafeAreaView style={styles.screen}><ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
    <Text accessibilityRole="header" style={styles.title}>Servers</Text>
    <Text style={styles.subtitle}>Connect this client to an Agent-Up Server.</Text>
    <View style={styles.card}>
      <Text style={styles.heading}>Add a server</Text>
      <Text style={styles.detail}>Enter an HTTP or HTTPS base URL. No credentials are stored.</Text>
      <Text style={styles.label}>Server URL</Text>
      <TextInput accessibilityLabel="Server URL" autoCapitalize="none" autoCorrect={false} keyboardType="url"
        placeholder="http://192.168.1.10:5000" placeholderTextColor="#718077" value={url} onChangeText={setUrl}
        onSubmitEditing={() => void tryAndSave()} style={styles.input} />
      <Pressable accessibilityRole="button" disabled={busy || !url.trim()} onPress={() => void tryAndSave()}
        style={[styles.button, (busy || !url.trim()) && styles.disabled]}>
        {busy ? <ActivityIndicator color="#000000" /> : <Text style={styles.buttonText}>Try and save</Text>}
      </Pressable>
      {!!status && <Text accessibilityRole="alert" style={styles.status}>{status}</Text>}
    </View>
    <View style={styles.current}><Text style={styles.currentLabel}>Current server</Text>
      <Text style={styles.currentUrl}>{activeServer?.url ?? 'No server selected'}</Text></View>
  </ScrollView></SafeAreaView>;
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#000000' }, content: { padding: 20, paddingTop: 78, paddingBottom: 32, gap: 18 },
  title: { color: '#f5fbf7', fontSize: 36, lineHeight: 40, fontWeight: '800' }, subtitle: { color: '#aebcb3', fontSize: 17 },
  card: { padding: 20, borderRadius: 8, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505', gap: 14 },
  heading: { color: '#f5fbf7', fontSize: 20, fontWeight: '700' }, detail: { color: '#aebcb3', lineHeight: 21 },
  label: { color: '#f5fbf7', fontWeight: '700' }, input: { minHeight: 50, borderRadius: 8, borderWidth: 1, borderColor: '#287038',
    paddingHorizontal: 14, color: '#f5fbf7', backgroundColor: '#080808' }, button: { minHeight: 48, alignItems: 'center', justifyContent: 'center', borderRadius: 8, backgroundColor: '#00d66b' },
  disabled: { opacity: 0.38 }, buttonText: { color: '#000000', fontWeight: '800' }, status: { color: '#2bf27a', lineHeight: 21 },
  current: { padding: 16, borderLeftWidth: 3, borderLeftColor: '#287038', gap: 4 }, currentLabel: { color: '#aebcb3', fontSize: 12, textTransform: 'uppercase' },
  currentUrl: { color: '#f5fbf7', fontWeight: '700' },
});
