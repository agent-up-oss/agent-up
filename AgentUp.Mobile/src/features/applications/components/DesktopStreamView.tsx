import { Platform, StyleSheet } from 'react-native';
import { WebView } from 'react-native-webview';

type DesktopStreamViewProps = { url: string };

export function DesktopStreamView({ url }: DesktopStreamViewProps) {
  if (Platform.OS === 'web') {
    return <iframe src={url} title="Desktop application" style={webStyle} />;
  }
  return <WebView source={{ uri: url }} style={styles.native} javaScriptEnabled allowsInlineMediaPlayback />;
}

const styles = StyleSheet.create({ native: { flex: 1, backgroundColor: '#111111' } });
const webStyle = { width: '100%', height: '100%', border: 0, background: '#111111' };
