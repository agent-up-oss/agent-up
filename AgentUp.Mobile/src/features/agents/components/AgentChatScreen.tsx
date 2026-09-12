import { useMemo, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { GitChangesPanel } from '@/features/git/components/GitChangesPanel';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

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
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { flex: 1 },
  chatContent: { padding: agentUpTheme.spacing[4], gap: agentUpTheme.spacing[3] },
  changesContent: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8] },
  chatTitle: auText('pageTitle'),
  chatPlaceholder: auText('muted'),
  bottomBar: {
    ...auBox('mobileTabBar'),
    flexDirection: 'row',
    gap: 10,
    paddingTop: 10,
  },
  tabButton: {
    ...auBox('subtab'),
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  tabButtonActive: auBox('subtabSelected'),
  tabLabel: auText('subtab'),
  tabLabelActive: auText('subtabSelected'),
});
