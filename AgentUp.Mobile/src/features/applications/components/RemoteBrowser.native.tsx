import { StyleSheet } from 'react-native';
import { WebView } from 'react-native-webview';
import type { ApplicationProxySource } from '../providers/ApplicationBrowserProvider';

/** Hosts the tunneled application HTTP response in the platform-native WebView. */
export function RemoteBrowser({ source }: { source: ApplicationProxySource }) {
  return (
    <WebView
      source={{ uri: source.uri, headers: { 'X-Agent-Up-Ticket': source.ticket } }}
      style={styles.browser}
      javaScriptEnabled
      domStorageEnabled
    />
  );
}

const styles = StyleSheet.create({ browser: { flex: 1, backgroundColor: '#050505' } });
