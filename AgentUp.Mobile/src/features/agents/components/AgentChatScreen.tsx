import { useMemo, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { GitChangesPanel } from '@/features/git/components/GitChangesPanel';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import type { Workspace } from '@/features/workspaces/models/Workspace';

type AgentTab = 'chat' | 'changes';

type AgentChatScreenProps = {
  workspace: Workspace;
};

export function AgentChatScreen({ workspace }: AgentChatScreenProps) {
  const insets = useSafeAreaInsets();
  const [tab, setTab] = useState<AgentTab>('chat');

  const shellConfig = useMemo(() => ({
    title: 'Workspace agent',
    rightAction: null,
    sidebarContent: null,
  }), []);

  useShellConfig(shellConfig);

  return (
    <View style={styles.screen}>
      <View style={styles.content}>
        {tab === 'chat'
          ? <ScrollView contentContainerStyle={styles.chatContent}>
              <Text style={styles.chatTitle}>{workspace.displayName}</Text>
              <Text style={styles.chatPlaceholder}>
                Agent chat will live here. This screen is reserved for the upcoming conversational interface.
              </Text>
            </ScrollView>
          : <ScrollView contentContainerStyle={styles.changesContent} keyboardShouldPersistTaps="handled">
              <GitChangesPanel />
            </ScrollView>}
      </View>
      <View style={[styles.bottomBar, { paddingBottom: insets.bottom + 8 }]}>
        <TabButton label="Chat" active={tab === 'chat'} onPress={() => setTab('chat')} />
        <TabButton label="Changes" active={tab === 'changes'} onPress={() => setTab('changes')} />
      </View>
    </View>
  );
}

function TabButton({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected: active }}
      accessibilityLabel={label}
      onPress={onPress}
      style={[styles.tabButton, active && styles.tabButtonActive]}>
      <Text style={[styles.tabLabel, active && styles.tabLabelActive]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#000000' },
  content: { flex: 1 },
  chatContent: { padding: 20, gap: 12 },
  changesContent: { padding: 20, paddingBottom: 32 },
  chatTitle: { color: '#f5fbf7', fontSize: 24, fontWeight: '800' },
  chatPlaceholder: { color: '#aebcb3', lineHeight: 22 },
  bottomBar: {
    flexDirection: 'row',
    gap: 10,
    paddingHorizontal: 14,
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: '#287038',
    backgroundColor: '#000000',
  },
  tabButton: {
    flex: 1,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#287038',
    backgroundColor: '#050505',
  },
  tabButtonActive: { borderColor: '#2bf27a', backgroundColor: '#08150d' },
  tabLabel: { color: '#aebcb3', fontWeight: '700' },
  tabLabelActive: { color: '#2bf27a' },
});
