import { useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useServers } from '../controllers/ServersContext';
import { normalizeServerUrl, probeServer } from '../providers/ServerUrlProvider';
import { recordServerConnectionAudit } from '../providers/MobileAuditProvider';
import { getAuthenticationStatus, login, ensureCredentialTransportAllowed } from '../../authentication/providers/AuthenticationProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export function ServerSetupScreen() {
  const router = useRouter();
  const { activeServer, servers, saveServer, selectServer, removeServer, requiresSignIn } = useServers();
  const [url, setUrl] = useState('');
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(false);
  const [password, setPassword] = useState('');
  const [loginUrl, setLoginUrl] = useState<string | null>(null);
  const connectionInFlight = useRef(false);

  useEffect(() => {
    if (!requiresSignIn || !activeServer) return;
    setUrl(activeServer.url);
    setLoginUrl(activeServer.url);
    setStatus('This saved sign-in is no longer valid. Enter the administrator password.');
  }, [requiresSignIn, activeServer]);

  const tryAndSave = async (candidate = url) => {
    if (connectionInFlight.current) return;
    connectionInFlight.current = true;
    setBusy(true); setStatus('Trying server…');
    setLoginUrl(null);
    setPassword('');
    try {
      const normalized = normalizeServerUrl(candidate);
      const auth = await getAuthenticationStatus(normalized);
      if (auth.authenticationRequired) {
        ensureCredentialTransportAllowed(normalized);
        setUrl(normalized);
        setLoginUrl(normalized);
        setStatus('Enter the server admin password.');
        return;
      }
      await probeServer(normalized);
      void recordServerConnectionAudit(normalized, 'success');
      saveServer(normalized); setUrl(''); setStatus(`Connected to ${normalized}`); router.replace('/(main)/workspace');
    } catch (error) {
      const fallback = tryNormalize(candidate);
      if (fallback) void recordServerConnectionAudit(fallback, 'failure', error instanceof Error ? error.message : String(error));
      setStatus(error instanceof Error ? error.message : 'Could not connect to the server.');
    } finally { connectionInFlight.current = false; setBusy(false); }
  };

  const openSaved = (id: string) => {
    const saved = servers.find(server => server.id === id);
    if (!saved || busy) return;
    if (saved.accessToken) {
      selectServer(saved.id);
      router.replace('/(main)/workspace');
      return;
    }
    setUrl(saved.url);
    void tryAndSave(saved.url);
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
    <View style={styles.card}>
      <Text style={styles.eyebrow}>Agent-Up Server</Text>
      <Text accessibilityRole="header" style={styles.title}>{loginUrl ? 'Sign in' : 'Connect to server'}</Text>
      <Text style={styles.subtitle}>
        {loginUrl
          ? 'Enter the administrator password to continue.'
          : 'Sign in to an Agent-Up Server to open your workspaces.'}
      </Text>
      <Text style={styles.label}>Server URL</Text>
      <TextInput accessibilityLabel="Server URL" autoCapitalize="none" autoCorrect={false} keyboardType="url"
        placeholder="https://agent-up.example.com" placeholderTextColor={agentUpTheme.colors.textFaint} value={url} onChangeText={setUrl}
        editable={!busy} onSubmitEditing={() => void tryAndSave()} style={styles.input} />
      {loginUrl && <>
        <Text style={styles.label}>Admin password</Text>
        <TextInput accessibilityLabel="Admin password" secureTextEntry value={password} onChangeText={setPassword}
          editable={!busy} onSubmitEditing={() => void signIn()} style={styles.input} /></>}
      {!loginUrl
        ? <Pressable accessibilityRole="button" disabled={busy || !url.trim()} onPress={() => void tryAndSave()}
            style={[styles.button, (busy || !url.trim()) && styles.disabled]}>
            {busy ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.buttonText}>Try and save</Text>}
          </Pressable>
        : <Pressable accessibilityRole="button" disabled={busy || !password} onPress={() => void signIn()}
            style={[styles.button, (busy || !password) && styles.disabled]}>
            {busy ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.buttonText}>Sign in</Text>}
          </Pressable>}
      {!!status && <Text accessibilityRole="alert" style={requiresSignIn ? styles.errorStatus : styles.status}>{status}</Text>}
    </View>
    {servers.length > 0 && <View style={styles.card}>
      <Text style={styles.heading}>Saved servers</Text>
      <Text style={styles.detail}>Switching replaces this client's local workspace and browser state. Saved sign-in tokens stay on this device.</Text>
      {servers.map(server => {
        const isActive = server.id === activeServer?.id;
        return (
          <View key={server.id} style={[styles.savedRow, isActive && styles.savedRowActive]}>
            <Pressable accessibilityRole="button" accessibilityState={{ selected: isActive }}
              accessibilityLabel={`Use server ${server.url}`} onPress={() => openSaved(server.id)}
              disabled={busy} style={styles.savedButton}>
              <Text numberOfLines={2} style={styles.savedUrl}>{server.url}</Text>
              <Text style={styles.savedMeta}>{isActive ? 'Current' : server.accessToken ? 'Saved sign-in' : 'No saved sign-in'}</Text>
            </Pressable>
            <Pressable accessibilityRole="button" accessibilityLabel={`Remove ${server.url}`}
              onPress={() => removeServer(server.id)} disabled={busy} style={styles.removeButton}>
              <Text style={styles.removeText}>Remove</Text>
            </Pressable>
          </View>
        );
      })}
    </View>}
    <View style={styles.current}>
      <Text style={styles.currentLabel}>Current server</Text>
      <Text style={styles.currentUrl}>{activeServer?.url ?? 'No server selected'}</Text>
    </View>
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
  content: {
    flexGrow: 1,
    alignItems: 'center',
    padding: agentUpTheme.spacing[6],
    paddingBottom: agentUpTheme.spacing[8],
    gap: agentUpTheme.spacing[3],
  },
  card: { ...auBox('signIn'), width: '100%', maxWidth: 416, gap: agentUpTheme.spacing[4] },
  eyebrow: auText('eyebrow'),
  title: auText('pageTitle'),
  subtitle: auText('muted'),
  heading: auText('pageTitle'),
  detail: auText('muted'),
  label: auText('fieldLabel'),
  input: { ...auBox('input'), ...auText('input') },
  button: { ...auBox('button'), alignItems: 'center', justifyContent: 'center' },
  disabled: auBox('buttonDisabled'),
  buttonText: auText('button'),
  status: auText('accent'),
  errorStatus: auText('badgeDanger'),
  current: { width: '100%', maxWidth: 416, ...auBox('workspace'), gap: agentUpTheme.spacing[1] },
  currentLabel: auText('fieldLabel'),
  currentUrl: auText('muted'),
  savedRow: { ...auBox('workspace'), flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  savedRowActive: auBox('workspaceSelected'),
  savedButton: { flex: 1, gap: 4 },
  savedUrl: auText('workspaceName'),
  savedMeta: auText('workspaceBranch'),
  removeButton: { minHeight: 40, justifyContent: 'center', paddingHorizontal: 8 },
  removeText: auText('badgeDanger'),
});
