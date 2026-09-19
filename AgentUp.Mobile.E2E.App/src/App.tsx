import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import * as Linking from 'expo-linking';
import { AgentChatScreen } from '@agent-up/chat';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { parseConnectLaunchUrl } from './connectLaunch';

/**
 * A harness, not a product.
 *
 * The sign-in tests used to drive the whole client to reach the chat: connect, open the sidebar,
 * pick a workspace, open its dashboard, then open the agent. Four of those five steps exercise
 * navigation that has nothing to do with signing an agent in, and every one of them was a way for
 * an unrelated change to break the sign-in suite.
 *
 * So this app is the chat module and the sign-in module and nothing else. It takes the Server and
 * the workspace it should talk about, and mounts the chat directly. The code under test is the
 * same module the real client mounts - the only thing missing here is the app around it.
 *
 * Native Detox opens agent-up-chat://connect so those two values arrive without the form. The
 * form stays for the installable web suite and for a person running the harness.
 */
export function App() {
  const [url, setUrl] = useState('');
  const [workspaceId, setWorkspaceId] = useState('');
  const [connected, setConnected] = useState<{ url: string; workspaceId: string } | null>(null);

  useEffect(() => {
    const apply = (href: string | null) => {
      const next = parseConnectLaunchUrl(href);
      if (next) setConnected(next);
    };
    const subscription = Linking.addEventListener('url', event => apply(event.url));
    void Linking.getInitialURL().then(apply);
    return () => subscription.remove();
  }, []);

  if (connected) {
    return <SafeAreaProvider>
      <StatusBar style="light" />
      <View style={styles.screen}>
        <AgentChatScreen
          workspace={{ id: connected.workspaceId, displayName: connected.workspaceId }}
          server={{ url: connected.url }}
        />
      </View>
    </SafeAreaProvider>;
  }

  return <SafeAreaProvider>
    <StatusBar style="light" />
    <View style={[styles.screen, styles.connect]}>
      <Text style={styles.title}>Agent-Up chat harness</Text>
      <TextInput
        testID="server-url-input"
        accessibilityLabel="Server URL"
        style={styles.input}
        autoCapitalize="none"
        autoCorrect={false}
        placeholder="http://localhost:5000"
        placeholderTextColor={agentUpTheme.colors.textFaint}
        value={url}
        onChangeText={setUrl}
      />
      <TextInput
        testID="workspace-id-input"
        accessibilityLabel="Workspace id"
        style={styles.input}
        autoCapitalize="none"
        autoCorrect={false}
        placeholder="workspace id"
        placeholderTextColor={agentUpTheme.colors.textFaint}
        value={workspaceId}
        onChangeText={setWorkspaceId}
      />
      <Pressable
        testID="server-connect"
        accessibilityRole="button"
        style={styles.connectButton}
        onPress={() => {
          const server = url.trim();
          const workspace = workspaceId.trim();
          if (server && workspace) setConnected({ url: server.replace(/\/+$/, ''), workspaceId: workspace });
        }}
      >
        <Text style={styles.connectLabel}>Connect</Text>
      </Pressable>
    </View>
  </SafeAreaProvider>;
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.surface },
  connect: { padding: 24, gap: 16, justifyContent: 'center' },
  title: auText('pageTitle'),
  input: { ...auBox('input'), ...auText('input') },
  connectButton: { ...auBox('button'), alignItems: 'center' },
  connectLabel: auText('button'),
});
