import { useCallback, useMemo, useState } from 'react';
import { Modal, Pressable, Text, TextInput, View } from 'react-native';
import { WebView } from 'react-native-webview';
import { resolveAction, runAction, submitCode as submitCodeToServer } from '@agent-up/agent-auth';
import type { AgentLoginApi, AgentLoginChallenge } from '@agent-up/agent-auth';

import { createAgentLoginPort } from '../providers/AgentLoginPortProvider';
import type { PendingRedirect } from '../providers/AgentLoginPortProvider';
import { shouldInterceptNavigation } from '../providers/AgentLoginRedirectProvider';
import { styles } from './AgentSignIn.styles';

export type AgentSignInProps = {
  challenge: AgentLoginChallenge | null | undefined;
  api: AgentLoginApi;
  copy: (value: string) => Promise<unknown>;
  onError: (message: string) => void;
};

/**
 * The sign-in panel.
 *
 * It branches on the transport the Server reported and never on which agent is signing in, so the
 * real Claude, Codex, and Cursor CLIs and the test agents all drive this same component.
 */
export function AgentSignIn({ challenge, api, copy, onError }: AgentSignInProps) {
  const [pendingRedirect, setPendingRedirect] = useState<PendingRedirect | null>(null);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);

  const port = useMemo(
    () => createAgentLoginPort({ requestRedirectWebView: setPendingRedirect, copy }),
    [copy],
  );

  const action = resolveAction(challenge);

  const start = useCallback(async () => {
    setBusy(true);
    try {
      const outcome = await runAction(action, port, api);
      if (outcome.kind === 'abandoned') onError('The sign-in was closed before it finished.');
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setBusy(false);
    }
  }, [action, api, onError, port]);

  const sendCode = useCallback(async () => {
    setBusy(true);
    try {
      if (!(await submitCodeToServer(code, api))) {
        onError('Enter the code the sign-in page gave you.');
        return;
      }
      setCode('');
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setBusy(false);
    }
  }, [api, code, onError]);

  const finishRedirect = useCallback(
    (callbackUrl: string | null) => {
      pendingRedirect?.settle(callbackUrl);
      setPendingRedirect(null);
    },
    [pendingRedirect],
  );

  if (action.kind === 'wait') {
    return (
      <View style={styles.panel}>
        <Text testID="agent-signin-instructions" style={styles.instructions}>{action.message}</Text>
      </View>
    );
  }

  return (
    <View style={styles.panel}>
      <Text testID="agent-signin-instructions" style={styles.instructions}>{action.message}</Text>

      <Pressable testID="agent-signin-open" accessibilityRole="button" disabled={busy} style={styles.primary} onPress={() => void start()}>
        <Text style={styles.primaryText}>Open the sign-in page</Text>
      </Pressable>

      <Pressable
        testID="agent-signin-copy-link"
        accessibilityRole="button"
        style={styles.secondary}
        onPress={() => void copy(action.url)}
      >
        <Text testID="agent-signin-url" style={styles.url} numberOfLines={2}>{action.url}</Text>
      </Pressable>

      {action.kind === 'openWithCode' && (
        <Pressable
          testID="agent-signin-copy-code"
          accessibilityRole="button"
          style={styles.secondary}
          onPress={() => void copy(action.code)}
        >
          <Text testID="agent-signin-code" style={styles.code}>{action.code}</Text>
        </Pressable>
      )}

      {action.kind === 'collectCode' && (
        <View style={styles.codeEntry}>
          <TextInput
            testID="agent-signin-code-input"
            style={styles.input}
            value={code}
            onChangeText={setCode}
            placeholder="Paste the code from the page"
            autoCapitalize="none"
            autoCorrect={false}
            editable={!busy}
          />
          <Pressable
            testID="agent-signin-submit-code"
            accessibilityRole="button"
            disabled={busy || code.trim().length === 0}
            style={styles.primary}
            onPress={() => void sendCode()}
          >
            <Text style={styles.primaryText}>Finish signing in</Text>
          </Pressable>
        </View>
      )}

      <Modal visible={pendingRedirect !== null} animationType="slide" onRequestClose={() => finishRedirect(null)}>
        <View style={styles.modal}>
          <Pressable testID="agent-signin-close-webview" accessibilityRole="button" style={styles.secondary} onPress={() => finishRedirect(null)}>
            <Text style={styles.secondaryText}>Cancel</Text>
          </Pressable>
          {pendingRedirect && (
            <WebView
              testID="agent-signin-webview"
              source={{ uri: pendingRedirect.url }}
              // The redirect targets loopback on the Server host. Following it here would only
              // reach the phone's own loopback, so it is stopped and handed back instead.
              onShouldStartLoadWithRequest={request => {
                if (!shouldInterceptNavigation(request.url, pendingRedirect.redirectUri)) return true;
                finishRedirect(request.url);
                return false;
              }}
            />
          )}
        </View>
      </Modal>
    </View>
  );
}
