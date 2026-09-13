import { StyleSheet, View } from 'react-native';

/** Hosts the tunneled application HTTP response in the installable web client. */
export function RemoteBrowser({ source }: { source: string }) {
  return (
    <View style={styles.container}>
      <iframe
        allow="clipboard-read; clipboard-write"
        src={source}
        style={{ border: 0, width: '100%', height: '100%' }}
        title="Application"
      />
    </View>
  );
}

const styles = StyleSheet.create({ container: { flex: 1, minHeight: 400, backgroundColor: '#050505' } });
