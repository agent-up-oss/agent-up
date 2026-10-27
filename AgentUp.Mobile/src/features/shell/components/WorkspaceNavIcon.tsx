import type { WorkspaceOverviewTab } from '../providers/WorkspaceTabProvider';
import { PublicIcon } from './PublicIcon';

type WorkspaceNavIconProps = {
  tab: WorkspaceOverviewTab;
  selected: boolean;
};

export function WorkspaceNavIcon({ tab, selected }: WorkspaceNavIconProps) {
  return <PublicIcon name={tab} muted={!selected} size={20} />;
}
