import { useRouter } from 'expo-router';
import { useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useServers } from '../controllers/ServersContext';
import { normalizeServerUrl, probeServer } from '../providers/ServerUrlProvider';
import { recordServerConnectionAudit } from '../providers/MobileAuditProvider';
import { getAuthenticationStatus, login, ensureCredentialTransportAllowed } from '../../authentication/providers/AuthenticationProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export function ServerSetupScreen() {
  const router = useRouter();
  const { activeServer, saveServer } = useServers();
  const [url, setUrl] = useState('');
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(false);
  const [password, setPassword] = useState('');
  const [loginUrl, setLoginUrl] = useState<string | null>(null);
  const connectionInFlight = useRef(false);

  const tryAndSave = async () => {
    if (connectionInFlight.current) return;
    connectionInFlight.current = true;
    setBusy(true); setStatus('Trying server…');
    setLoginUrl(null);
    setPassword('');
    try {
      const normalized = normalizeServerUrl(url);
      const auth = await getAuthenticationStatus(normalized);
      if (auth.authenticationRequired) {
        ensureCredentialTransportAllowed(normalized);
        setLoginUrl(normalized);
        setStatus('Enter the server admin password.');
        return;
      }
      await probeServer(normalized);
      void recordServerConnectionAudit(normalized, 'success');
      saveServer(normalized); setUrl(''); setStatus(`Connected to ${normalized}`); router.replace('/(main)/workspace');
    } catch (error) {
      const candidate = tryNormalize(url);
      if (candidate) void recordServerConnectionAudit(candidate, 'failure', error instanceof Error ? error.message : String(error));
      setStatus(error instanceof Error ? error.message : 'Could not connect to the server.');
    } finally { connectionInFlight.current = false; setBusy(false); }
  };

  const signIn = async () => {
    if (!loginUrl || busy) return;
    setBusy(true);
    try {
      const result = await login(loginUrl, password);
      if (!result.accessToken) throw new Error('The server did not return an access token.');
      saveServer(loginUrl, result.accessToken);
      setPassword(''); setLoginUrl(null); setUrl(''); setStatus(`Signed in to ${loginUrl}`); router.replace('/(main)/workspace');
    } catch (error) { setStatus(error instanceof Error ? error.message : 'Could not sign in.'); }
    finally { setBusy(false); connectionInFlight.current = false; }
  };

  return <SafeAreaView style={styles.screen}><ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
    <Text accessibilityRole="header" style={styles.title}>Connect to server</Text>
    <Text style={styles.subtitle}>Sign in to an Agent-Up Server to open your workspaces.</Text>
    <View style={styles.card}>
      <Text style={styles.heading}>Add a server</Text>
      <Text style={styles.detail}>Use HTTPS for remote servers. Loopback HTTP URLs are allowed for local development. Login tokens stay in this client's local storage.</Text>
      <Text style={styles.label}>Server URL</Text>
      <TextInput accessibilityLabel="Server URL" autoCapitalize="none" autoCorrect={false} keyboardType="url"
        placeholder="https://agent-up.example.com" placeholderTextColor={agentUpTheme.colors.textFaint} value={url} onChangeText={setUrl}
        editable={!busy} onSubmitEditing={() => void tryAndSave()} style={styles.input} />
      {loginUrl && <><Text style={styles.label}>Admin password</Text>
        <TextInput accessibilityLabel="Admin password" secureTextEntry value={password} onChangeText={setPassword}
          editable={!busy} onSubmitEditing={() => void signIn()} style={styles.input} /></>}
      <Pressable accessibilityRole="button" disabled={busy || !url.trim()} onPress={() => void tryAndSave()}
        style={[styles.button, (busy || !url.trim()) && styles.disabled]}>
        {busy ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.buttonText}>Try and save</Text>}
      </Pressable>
      {loginUrl && <Pressable accessibilityRole="button" disabled={busy || !password} onPress={() => void signIn()}
        style={[styles.button, (busy || !password) && styles.disabled]}>
        <Text style={styles.buttonText}>Sign in</Text></Pressable>}
      {!!status && <Text accessibilityRole="alert" style={styles.status}>{status}</Text>}
    </View>
    <View style={styles.current}><Text style={styles.currentLabel}>Current server</Text>
      <Text style={styles.currentUrl}>{activeServer?.url ?? 'No server selected'}</Text></View>
  </ScrollView></SafeAreaView>;
}

function tryNormalize(value: string): string | null {
  try { return normalizeServerUrl(value); }
  catch (error) {
    if (error instanceof Error) return null;
    throw error;
  }
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[5], paddingTop: agentUpTheme.spacing[6], paddingBottom: agentUpTheme.spacing[8], gap: 18 },
  title: auText('title'),
  subtitle: auText('lede'),
  card: { ...auBox('signIn'), gap: 14 },
  heading: auText('heading'),
  detail: auText('muted'),
  label: { ...auText('heading'), fontSize: agentUpTheme.typography.sizeSm },
  input: auBox('input'),
  button: { ...auBox('button'), alignItems: 'center', justifyContent: 'center' },
  disabled: { opacity: 0.38 },
  buttonText: auText('button'),
  status: auText('accent'),
  current: auBox('callout'),
  currentLabel: auText('eyebrow'),
  currentUrl: auText('heading'),
});
