import Ionicons from '@expo/vector-icons/Ionicons';
import { agentUpTheme } from '@agent-up/design-system/native';
import type { WorkspaceOverviewTab } from '../providers/WorkspaceTabProvider';

const names = {
  apps: 'grid-outline',
  git: 'git-branch-outline',
  agents: 'chatbubbles-outline',
  settings: 'settings-outline',
} as const;

type WorkspaceNavIconProps = {
  tab: WorkspaceOverviewTab;
  selected: boolean;
};

export function WorkspaceNavIcon({ tab, selected }: WorkspaceNavIconProps) {
  return (
    <Ionicons
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      name={names[tab]}
      size={20}
      color={selected ? agentUpTheme.colors.textPrimary : agentUpTheme.colors.textMuted}
    />
  );
}
