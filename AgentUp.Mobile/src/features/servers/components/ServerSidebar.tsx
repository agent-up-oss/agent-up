import { useRouter } from 'expo-router';
import { useState, type PropsWithChildren } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useServers } from '../controllers/ServersContext';

export function ServerSidebar({ children }: PropsWithChildren) {
  const [open, setOpen] = useState(false);
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { servers, activeServer, selectServer } = useServers();

  const goHome = () => { setOpen(false); router.navigate('/'); };
  return <View style={styles.shell}>
    {children}
    <Pressable accessibilityRole="button" accessibilityLabel="Open servers" onPress={() => setOpen(true)}
      style={[styles.stackButton, { top: insets.top + 10 }]}>
      <Text style={styles.stackIcon}>▰</Text><Text style={styles.stackIcon}>▰</Text><Text style={styles.stackIcon}>▰</Text>
    </Pressable>
    <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
      <View style={styles.modal}>
        <Pressable accessibilityLabel="Close servers" style={styles.scrim} onPress={() => setOpen(false)} />
        <View style={[styles.sidebar, { paddingTop: insets.top + 18, paddingBottom: insets.bottom + 18 }]}>
          <Text style={styles.heading}>Servers</Text>
          <View style={styles.serverList}>
            {servers.map((server, index) => <Pressable key={server.id} accessibilityRole="button"
              accessibilityLabel={`Connect to ${server.url}`} onPress={() => { selectServer(server.id); setOpen(false); }}
              style={[styles.serverIcon, activeServer?.id === server.id && styles.activeIcon]}>
              <Text numberOfLines={1} style={styles.iconText}>{index + 1}</Text>
            </Pressable>)}
            <Pressable accessibilityRole="button" accessibilityLabel="Add server" onPress={goHome} style={[styles.serverIcon, styles.addIcon]}>
              <Text style={styles.plus}>+</Text>
            </Pressable>
          </View>
          <View style={styles.selected}><Text style={styles.selectedLabel}>Connected server</Text>
            <Text numberOfLines={3} style={styles.selectedUrl}>{activeServer?.url ?? 'None configured'}</Text></View>
        </View>
      </View>
    </Modal>
  </View>;
}

const styles = StyleSheet.create({
  shell: { flex: 1 }, stackButton: { position: 'absolute', left: 14, zIndex: 20, width: 42, height: 42,
    alignItems: 'center', justifyContent: 'center', borderRadius: 8, borderWidth: 1, borderColor: '#287038', backgroundColor: '#050505' },
  stackIcon: { color: '#2bf27a', fontSize: 10, lineHeight: 8 }, modal: { flex: 1, flexDirection: 'row' },
  scrim: { position: 'absolute', inset: 0, backgroundColor: 'rgba(0,0,0,0.68)' }, sidebar: { width: 112, height: '100%',
    alignItems: 'center', paddingHorizontal: 12, gap: 18, backgroundColor: '#050505', borderRightWidth: 1, borderRightColor: '#287038' },
  heading: { color: '#f5fbf7', fontWeight: '800' }, serverList: { alignItems: 'center', gap: 12 },
  serverIcon: { width: 58, height: 58, borderRadius: 29, alignItems: 'center', justifyContent: 'center', backgroundColor: '#16231a', borderWidth: 2, borderColor: 'transparent' },
  activeIcon: { borderColor: '#2bf27a', borderRadius: 18 }, addIcon: { borderWidth: 1, borderColor: '#287038', backgroundColor: '#080808' },
  iconText: { color: '#f5fbf7', fontWeight: '800', fontSize: 18 }, plus: { color: '#2bf27a', fontSize: 30, lineHeight: 34 },
  selected: { marginTop: 'auto', width: '100%', gap: 4 }, selectedLabel: { color: '#aebcb3', fontSize: 10, textTransform: 'uppercase' },
  selectedUrl: { color: '#f5fbf7', fontSize: 11, lineHeight: 15 },
});
