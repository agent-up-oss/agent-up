import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { WorkspaceTabBar } from './WorkspaceTabBar';
import type { WorkspaceOverviewTab } from '../providers/WorkspaceTabProvider';

/**
 * The row of tabs under a workspace screen, where the shell around it draws one.
 *
 * The classic shell does, and gets exactly the bar it always had. A shell that carries its own
 * tabs draws nothing here rather than stacking a second row beneath its own.
 */
export function WorkspaceTabs({
  workspaceId,
  active,
}: {
  workspaceId: string;
  active: WorkspaceOverviewTab;
}) {
  const host = useWorkspaceHost(workspaceId);
  if (!host.showsTabBar) return null;
  return <WorkspaceTabBar workspaceId={workspaceId} active={active} />;
}
