import { useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useServers } from '../controllers/ServersContext';
import { hasSavedSignIn } from '../models/ConfiguredServer';
import { resolvePresetServerUrl, rememberPresetWorkspace, takePendingWorkspace, workspaceHref } from '../providers/PresetServerProvider';
import { normalizeServerUrl, probeServer } from '../providers/ServerUrlProvider';
import { recordServerConnectionAudit } from '../providers/MobileAuditProvider';
import { getAuthenticationStatus, getConnection, login, ensureCredentialTransportAllowed } from '../../authentication/providers/AuthenticationProvider';
import { browserSsoStartUrl, createSsoState, readSsoCallback, rememberSsoStart, takePendingSsoStart, usesBrowserSso } from '../../authentication/providers/BrowserSsoProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { fakeServers } from '@/features/fake-server/controllers/FakeServerController';
import { fakeServerDisplayName } from '@/features/fake-server/models/FakeServerIdentity';

type FormMode = 'add' | 'password' | 'cloud' | 'sso';

export function ServerSetupScreen({ presetServerUrl, presetWorkspaceId }: { presetServerUrl?: string; presetWorkspaceId?: string }) {
  const router = useRouter();
  const { activeServer, savedServers, cloudServer, servers, saveServer, selectServer, removeServer, requiresSignIn, ready } = useServers();
  const [url, setUrl] = useState('');
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(false);
  const [password, setPassword] = useState('');
  const [loginUrl, setLoginUrl] = useState<string | null>(cloudServer?.url ?? null);
  const [formMode, setFormMode] = useState<FormMode>(cloudServer ? 'cloud' : 'add');
  const [ssoDisplayName, setSsoDisplayName] = useState('this Server');
  const [ssoPrompt, setSsoPrompt] = useState('');
  const connectionInFlight = useRef(false);
  const appliedPreset = useRef(false);

  const showAddForm = () => {
    setFormMode('add');
    setLoginUrl(null);
    setPassword('');
    setStatus('');
    setUrl('');
  };

  const showCloudLogin = () => {
    if (!cloudServer) return;
    setFormMode('cloud');
    setLoginUrl(cloudServer.url);
    setPassword('');
    setUrl('');
    setStatus('');
  };

  const showBrowserSso = (serverUrl: string, displayName: string, prompt: string) => {
    setFormMode('sso');
    setLoginUrl(serverUrl);
    setSsoDisplayName(displayName || 'this Server');
    setSsoPrompt(prompt);
    setUrl(serverUrl);
    setPassword('');
    setStatus('');
  };

  useEffect(() => {
    if (typeof window === 'undefined') return;
    const callback = readSsoCallback(window.location.href);
    if (!callback) return;
    const pending = takePendingSsoStart();
    if (!pending || pending.state !== callback.state) return;
    saveServer(pending.serverUrl, callback.accessToken);
    window.history.replaceState({}, '', window.location.pathname + window.location.hash);
    router.replace(workspaceHref(takePendingWorkspace() ?? presetWorkspaceId));
  }, []);

  useEffect(() => {
    if (!ready || appliedPreset.current) return;
    if (presetWorkspaceId) rememberPresetWorkspace(presetWorkspaceId);
    if (!presetServerUrl) return;
    appliedPreset.current = true;
    try {
      const target = resolvePresetServerUrl(presetServerUrl, cloudServer?.url);
      if (target.kind === 'cloud') {
        if (cloudServer && hasSavedSignIn(cloudServer)) {
          selectServer(cloudServer.id);
          router.replace(workspaceHref(presetWorkspaceId));
          return;
        }
        showCloudLogin();
        return;
      }
      const saved = savedServers.find(server => server.url === target.url);
      if (saved && hasSavedSignIn(saved)) {
        selectServer(saved.id);
        router.replace(workspaceHref(presetWorkspaceId));
        return;
      }
      setUrl(target.url);
      setLoginUrl(saved ? target.url : null);
      setFormMode(saved ? 'password' : 'add');
      setStatus('');
    } catch {
      setStatus('The linked server URL is not valid.');
    }
  }, [ready, presetServerUrl, presetWorkspaceId, cloudServer, savedServers, router, selectServer]);

  useEffect(() => {
    if (!requiresSignIn || !activeServer) return;
    if (activeServer.isRecommended) {
      showCloudLogin();
      return;
    }
    setUrl(activeServer.url);
    setFormMode('password');
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
      if (fakeServers.matches(normalized)) {
        saveServer(normalized);
        setUrl('');
        setStatus('Connected to Demo');
        router.replace(workspaceHref(presetWorkspaceId));
        return;
      }
      if (cloudServer && normalized === cloudServer.url) {
        showCloudLogin();
        return;
      }
      const connection = await getConnection(normalized);
      const auth = await getAuthenticationStatus(normalized);
      if (auth.authenticationRequired) {
        ensureCredentialTransportAllowed(normalized);
        if (usesBrowserSso(connection) && connection) {
          showBrowserSso(normalized, connection.displayName, connection.authentication.prompt);
          return;
        }
        setUrl(normalized);
        setLoginUrl(normalized);
        setFormMode('password');
        setStatus(connection?.authentication.prompt || 'Enter the administrator password to continue.');
        return;
      }
      await probeServer(normalized);
      void recordServerConnectionAudit(normalized, 'success');
      saveServer(normalized); setUrl(''); setStatus(`Connected to ${normalized}`); router.replace(workspaceHref(presetWorkspaceId));
    } catch (error) {
      const fallback = tryNormalize(candidate);
      if (fallback) void recordServerConnectionAudit(fallback, 'failure', error instanceof Error ? error.message : String(error));
      setFormMode('add');
      setStatus(error instanceof Error ? error.message : 'Could not connect to the server.');
    } finally { connectionInFlight.current = false; setBusy(false); }
  };

  const openSaved = (id: string) => {
    const saved = servers.find(server => server.id === id);
    if (!saved || busy) return;
    if (saved.isFake) {
      selectServer(saved.id);
      router.replace(workspaceHref(presetWorkspaceId));
      return;
    }
    if (hasSavedSignIn(saved)) {
      selectServer(saved.id);
      router.replace(workspaceHref(presetWorkspaceId));
      return;
    }
    setUrl(saved.url);
    void tryAndSave(saved.url);
  };

  const openCloud = () => {
    if (!cloudServer || busy) return;
    if (hasSavedSignIn(cloudServer)) {
      selectServer(cloudServer.id);
      router.replace(workspaceHref(presetWorkspaceId));
      return;
    }
    showCloudLogin();
  };

  const startSso = (serverUrl: string) => {
    rememberPresetWorkspace(presetWorkspaceId);
    if (Platform.OS === 'web' && typeof window !== 'undefined') {
      try {
        const redirect = `${window.location.origin}${window.location.pathname}`;
        const state = createSsoState();
        const target = browserSsoStartUrl(serverUrl, redirect, state);
        rememberSsoStart(serverUrl, state);
        window.location.assign(target.href);
      } catch {
        setStatus('The server URL is invalid. Please check it and try again.');
      }
      return;
    }
    setStatus('Continue sign-in in the browser from the web client.');
  };

  const signIn = async () => {
    if (!loginUrl || busy) return;
    if (formMode === 'cloud' || formMode === 'sso') {
      startSso(loginUrl);
      return;
    }
    setBusy(true);
    try {
      const result = await login(loginUrl, password, fetch);
      if (!result.accessToken) throw new Error('The server did not return an access token.');
      saveServer(loginUrl, result.accessToken);
      setPassword(''); setLoginUrl(null); setUrl(''); setStatus(`Signed in to ${loginUrl}`); router.replace(workspaceHref(presetWorkspaceId));
    } catch (error) { setStatus(error instanceof Error ? error.message : 'Could not sign in.'); }
    finally { setBusy(false); connectionInFlight.current = false; }
  };

  const cloudName = cloudServer?.displayName ?? 'Agent-Up Cloud';

  return <SafeAreaView style={styles.screen}><ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
    <View style={styles.card}>
      {formMode === 'cloud' ? <>
        <Text style={styles.eyebrow}>{cloudName.toUpperCase()}</Text>
        <Text accessibilityRole="header" style={styles.title}>Login via {cloudName}</Text>
        <Pressable testID="server-sso-continue" accessibilityRole="button" disabled={busy} onPress={() => void signIn()}
          style={[styles.button, busy && styles.disabled]}>
          {busy ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.buttonText}>Continue</Text>}
        </Pressable>
      </> : formMode === 'sso' ? <>
        <Text style={styles.eyebrow}>BROWSER SIGN-IN</Text>
        <Text accessibilityRole="header" style={styles.title}>Sign in to {ssoDisplayName}</Text>
        <Text style={styles.subtitle}>{ssoPrompt || 'Continue in the browser to receive an access token.'}</Text>
        <Pressable testID="server-sso-continue" accessibilityRole="button" disabled={busy} onPress={() => void signIn()}
          style={[styles.button, busy && styles.disabled]}>
          {busy ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.buttonText}>Continue</Text>}
        </Pressable>
      </> : <>
        <Text style={styles.eyebrow}>Agent-Up Server</Text>
        <Text accessibilityRole="header" style={styles.title}>{formMode === 'password' ? 'Sign in' : 'Connect to server'}</Text>
        <Text style={styles.subtitle}>
          {formMode === 'password'
            ? 'Enter the administrator password to continue.'
            : 'Choose a saved server or enter a URL. Switching replaces this client\'s local workspace and browser state.'}
        </Text>
        <Text style={styles.label}>Server URL</Text>
        <TextInput testID="server-url-input" accessibilityLabel="Server URL" autoCapitalize="none" autoCorrect={false} keyboardType="url"
          placeholder="https://agent-up.example.com" placeholderTextColor={agentUpTheme.colors.textFaint} value={url} onChangeText={setUrl}
          editable={!busy} onSubmitEditing={() => void tryAndSave()} style={styles.input} />
        {formMode === 'password' && <>
          <Text style={styles.label}>Password</Text>
          <TextInput accessibilityLabel="Password" secureTextEntry value={password} onChangeText={setPassword}
            editable={!busy} onSubmitEditing={() => void signIn()} style={styles.input} />
        </>}
        {formMode === 'add'
          ? <Pressable testID="server-connect" accessibilityRole="button" disabled={busy || !url.trim()} onPress={() => void tryAndSave()}
              style={[styles.button, (busy || !url.trim()) && styles.disabled]}>
              {busy ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.buttonText}>Try and save</Text>}
            </Pressable>
          : <Pressable accessibilityRole="button" disabled={busy || !password} onPress={() => void signIn()}
              style={[styles.button, (busy || !password) && styles.disabled]}>
              {busy ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.buttonText}>Sign in</Text>}
            </Pressable>}
      </>}
      {!!status && <Text accessibilityRole="alert" style={requiresSignIn ? styles.errorStatus : styles.status}>{status}</Text>}
      <Text style={styles.label}>Saved servers</Text>
      {servers.filter(server => server.isFake).map(server => {
        const isActive = server.id === activeServer?.id && formMode !== 'cloud';
        return (
          <Pressable key={server.id} accessibilityRole="button"
            accessibilityState={{ selected: isActive }}
            accessibilityLabel={fakeServerDisplayName}
            onPress={() => openSaved(server.id)} disabled={busy}
            style={[styles.savedRow, isActive && styles.savedRowActive]}>
            <Text numberOfLines={1} ellipsizeMode="tail" style={styles.savedUrl}>{fakeServerDisplayName}</Text>
          </Pressable>
        );
      })}
      {cloudServer && (
        <Pressable accessibilityRole="button"
          accessibilityState={{ selected: formMode === 'cloud' || activeServer?.id === cloudServer.id }}
          accessibilityLabel={cloudName}
          onPress={openCloud} disabled={busy}
          style={[styles.savedRow, (formMode === 'cloud' || activeServer?.isRecommended) && styles.savedRowActive]}>
          <Text numberOfLines={1} ellipsizeMode="tail" style={styles.savedUrl}>{cloudName}</Text>
        </Pressable>
      )}
      {savedServers.map(server => {
        const isActive = server.id === activeServer?.id && formMode !== 'cloud';
        return (
          <View key={server.id} style={styles.savedLine}>
            <Pressable accessibilityRole="button" accessibilityState={{ selected: isActive }}
              accessibilityLabel={`Use server ${server.displayName ?? server.url}`} onPress={() => openSaved(server.id)}
              disabled={busy} style={[styles.savedRow, isActive && styles.savedRowActive, styles.savedButton]}>
              <Text numberOfLines={1} ellipsizeMode="tail" style={styles.savedUrl}>{server.displayName ?? server.url}</Text>
            </Pressable>
            {server.canRemove !== false && (
            <Pressable accessibilityRole="button" accessibilityLabel={`Remove ${server.url}`}
              onPress={() => removeServer(server.id)} disabled={busy} style={styles.removeButton}>
              <Text style={styles.removeText}>Remove</Text>
            </Pressable>
            )}
          </View>
        );
      })}
      <Pressable accessibilityRole="button" accessibilityLabel="Add a server" onPress={showAddForm}
        disabled={busy} style={styles.addButton}>
        <Text style={styles.addButtonText}>Add server</Text>
      </Pressable>
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
    justifyContent: 'center',
    padding: agentUpTheme.spacing[6],
    paddingBottom: agentUpTheme.spacing[8],
  },
  card: { ...auBox('signIn'), width: '100%', maxWidth: 352, gap: agentUpTheme.spacing[3] },
  eyebrow: auText('eyebrow'),
  title: auText('pageTitle'),
  subtitle: auText('muted'),
  label: auText('fieldLabel'),
  input: { ...auBox('input'), ...auText('input') },
  button: { ...auBox('button'), alignItems: 'center', justifyContent: 'center' },
  disabled: auBox('buttonDisabled'),
  buttonText: auText('button'),
  status: auText('accent'),
  errorStatus: auText('badgeDanger'),
  savedLine: { flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  savedRow: { ...auBox('choice', 'choiceCompact'), flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  savedRowActive: auBox('choiceSelected'),
  savedButton: { flex: 1, minWidth: 0 },
  savedUrl: { ...auText('workspaceName'), ...auText('choiceLabel') },
  removeButton: { minHeight: 40, justifyContent: 'center', paddingHorizontal: 8 },
  removeText: auText('badgeDanger'),
  addButton: {
    ...auBox('button', 'buttonSecondary'),
    alignItems: 'center',
    justifyContent: 'center',
  },
  addButtonText: auText('buttonSecondary'),
});
