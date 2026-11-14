import { useMemo } from 'react';
import {
  useWorkspaceHost,
  type WorkspacePlace,
} from '@/features/workspaces/controllers/WorkspaceHostContext';
import type { AppShellConfig } from '../controllers/AppShellContext';

/**
 * The chrome for a screen opened on top of a workspace tab: its title, and a way back to the tab
 * it came from.
 *
 * This is <c>useInnerShell</c> with the shell around it asked for the way back rather than the
 * app's routes assumed, so the same screen reads the same whichever shell is hosting it.
 */
export function useWorkspaceInnerShell(
  workspaceId: string,
  title: string,
  fallback: WorkspacePlace,
): AppShellConfig {
  const host = useWorkspaceHost(workspaceId);
  return useMemo(() => ({
    title,
    rightAction: null,
    sidebarContent: null,
    backAction: {
      label: 'Back',
      accessibilityLabel: 'Go back',
      onPress: () => host.back(fallback),
    },
  }), [title, host, fallback]);
}
