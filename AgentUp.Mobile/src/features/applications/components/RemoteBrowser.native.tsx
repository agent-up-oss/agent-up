import { StyleSheet } from 'react-native';
import { WebView } from 'react-native-webview';

export function RemoteBrowser({ source }: { source: string }) {
  return <WebView source={{ uri: source }} style={styles.browser} javaScriptEnabled domStorageEnabled />;
}

const styles = StyleSheet.create({ browser: { flex: 1, backgroundColor: '#050505' } });
