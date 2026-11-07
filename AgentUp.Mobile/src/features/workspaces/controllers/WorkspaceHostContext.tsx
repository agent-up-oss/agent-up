import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useRouter } from 'expo-router';
import { resolveInnerBack } from '@/features/shell/providers/InnerShellBackProvider';

/**
 * Somewhere inside one workspace a screen can send the reader.
 *
 * The workspace screens know which of these they want; they do not know how the shell around them
 * gets there. The classic shell answers with a route, the command center with a page of its own,
 * and the screen itself reads the same either way.
 */
export type WorkspacePlace =
  | { kind: 'apps' }
  | { kind: 'git' }
  | { kind: 'agents' }
  | { kind: 'settings' }
  | { kind: 'application'; name: string }
  | { kind: 'gitHistory' }
  | { kind: 'gitReview' }
  | { kind: 'agent' }
  | { kind: 'agentPolicy' }
  | { kind: 'agentCreate' };

export type WorkspaceHost = {
  /**
   * Goes somewhere else in this workspace. Replacing leaves nothing behind to go back to, which
   * is what a screen wants once its own job is done and returning to it would be wrong.
   */
  open(place: WorkspacePlace, options?: { replace?: boolean }): void;
  /** Leaves a place that was opened on top of another, back to where it came from. */
  back(fallback: WorkspacePlace): void;
  /**
   * True where the classic bottom tab bar is how a reader moves between a workspace's tabs. A
   * shell that carries its own tabs says false rather than drawing a second row of them.
   */
  showsTabBar: boolean;
};

const WorkspaceHostContext = createContext<((workspaceId: string) => WorkspaceHost) | null>(null);

/**
 * Hands the screens inside it a different way of moving around a workspace. Without one they use
 * the app's routes, which is what the classic shell has always done.
 */
export function WorkspaceHostProvider({
  host,
  children,
}: {
  host: (workspaceId: string) => WorkspaceHost;
  children: ReactNode;
}) {
  return <WorkspaceHostContext.Provider value={host}>{children}</WorkspaceHostContext.Provider>;
}

/** How this screen moves around the workspace it is showing. */
export function useWorkspaceHost(workspaceId: string): WorkspaceHost {
  const provided = useContext(WorkspaceHostContext);
  const router = useRouter();

  return useMemo(() => {
    if (provided) return provided(workspaceId);
    const href = (place: WorkspacePlace) => `/(main)/workspace/${workspaceId}${suffix(place)}`;
    return {
      open: (place, options) => {
        // The tabs replace rather than stack, the way the tab bar always has: a tab is a
        // sideways move, so going back from one should leave the workspace, not walk its tabs.
        if (options?.replace || isTab(place)) router.replace(href(place) as never);
        else router.push(href(place) as never);
      },
      back: fallback => {
        if (resolveInnerBack(router.canGoBack()) === 'back') router.back();
        else router.replace(href(fallback) as never);
      },
      showsTabBar: true,
    };
  }, [provided, workspaceId, router]);
}

function isTab(place: WorkspacePlace): boolean {
  return place.kind === 'apps' || place.kind === 'git' || place.kind === 'agents' || place.kind === 'settings';
}

/** The route each place has in the app's own routing, which is the classic shell's answer. */
function suffix(place: WorkspacePlace): string {
  switch (place.kind) {
    case 'apps': return '';
    case 'git': return '/git';
    case 'agents': return '/agents';
    case 'settings': return '/settings';
    case 'application': return `/application/${encodeURIComponent(place.name)}`;
    case 'gitHistory': return '/git/history';
    case 'gitReview': return '/git/review';
    case 'agent': return '/agent';
    case 'agentPolicy': return '/agent-policy';
    case 'agentCreate': return '/agent-create';
  }
}
