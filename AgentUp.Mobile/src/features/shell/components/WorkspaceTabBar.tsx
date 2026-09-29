import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { auBox, auText } from '@agent-up/design-system/native';
import { WorkspaceNavIcon } from './WorkspaceNavIcon';
import { workspaceTabHref, type WorkspaceOverviewTab } from '../providers/WorkspaceTabProvider';

const tabs: { id: WorkspaceOverviewTab; label: string }[] = [
  { id: 'apps', label: 'Apps' },
  { id: 'git', label: 'Git' },
  { id: 'agents', label: 'Agents' },
  { id: 'settings', label: 'Settings' },
];

type WorkspaceTabBarProps = {
  workspaceId: string;
  active: WorkspaceOverviewTab;
};

export function WorkspaceTabBar({ workspaceId, active }: WorkspaceTabBarProps) {
  const router = useRouter();
  const insets = useSafeAreaInsets();

  return (
    <View accessibilityRole="tablist" style={[styles.bar, insets.bottom > 0 && { paddingBottom: insets.bottom }]}>
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
            <WorkspaceNavIcon tab={tab.id} selected={selected} />
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
    gap: 6,
  },
  tab: {
    ...auBox('subtab'),
    ...auBox('navTab'),
    flex: 1,
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 2,
  },
  tabSelected: auBox('subtabSelected'),
  label: auText('subtab'),
  labelSelected: auText('subtabSelected'),
});
