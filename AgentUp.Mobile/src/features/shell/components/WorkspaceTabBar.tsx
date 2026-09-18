import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { workspaceTabHref, type WorkspaceOverviewTab } from '../providers/WorkspaceTabProvider';

const tabs: { id: WorkspaceOverviewTab; label: string }[] = [
  { id: 'apps', label: 'Apps' },
  { id: 'git', label: 'Git' },
  { id: 'agents', label: 'Agents' },
];

type WorkspaceTabBarProps = {
  workspaceId: string;
  active: WorkspaceOverviewTab;
};

export function WorkspaceTabBar({ workspaceId, active }: WorkspaceTabBarProps) {
  const router = useRouter();
  const insets = useSafeAreaInsets();

  return (
    <View accessibilityRole="tablist" style={[styles.bar, { paddingBottom: insets.bottom + 8 }]}>
      {tabs.map(tab => {
        const selected = tab.id === active;
        return (
          <Pressable
            key={tab.id}
            testID={`workspace-tab-${tab.id}`}
            accessibilityRole="tab"
            accessibilityState={{ selected }}
            accessibilityLabel={tab.label}
            onPress={() => router.replace(workspaceTabHref(workspaceId, tab.id))}
            style={[styles.tab, selected && styles.tabSelected]}>
            <Text style={selected ? styles.labelSelected : styles.label}>{tab.label}</Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  bar: {
    ...auBox('mobileTabBar'),
    flexDirection: 'row',
    gap: 10,
    paddingHorizontal: 14,
    paddingTop: 10,
  },
  tab: { ...auBox('subtab'), flex: 1, alignItems: 'center', justifyContent: 'center' },
  tabSelected: auBox('subtabSelected'),
  label: auText('subtab'),
  labelSelected: auText('subtabSelected'),
});
