import { StyleSheet, View } from 'react-native';
import { agentUpTheme } from '@agent-up/design-system/native';
import type { ApplicationProxySource } from '../providers/ApplicationBrowserProvider';

/** Hosts the tunneled application HTTP response in the installable web client. */
export function RemoteBrowser({ source }: { source: ApplicationProxySource }) {
  return (
    <View style={styles.container}>
      <iframe
        allow="clipboard-read; clipboard-write"
        src={`${source.uri}#ticket=${encodeURIComponent(source.ticket)}`}
        style={{ border: 0, width: '100%', height: '100%' }}
        title="Application"
      />
    </View>
  );
}

const styles = StyleSheet.create({ container: { flex: 1, minHeight: 400, backgroundColor: agentUpTheme.colors.canvas } });
