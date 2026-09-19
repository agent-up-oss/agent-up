import type { Href } from 'expo-router';

export type WorkspaceOverviewTab = 'apps' | 'git' | 'agents';

export function workspacePathAfterId(pathname: string, workspaceId: string): string[] {
  const decoded = decodeURIComponent(workspaceId);
  const parts = pathname.split('/').filter(part => part.length > 0 && part !== '(main)');
  const index = parts.findIndex(part => part === workspaceId || decodeURIComponent(part) === decoded);
  if (index < 0) return [];
  return parts.slice(index + 1);
}

export function workspaceOverviewTab(pathname: string, workspaceId: string): WorkspaceOverviewTab | null {
  const rest = workspacePathAfterId(pathname, workspaceId);
  if (rest.length === 0) return 'apps';
  if (rest.length === 1 && rest[0] === 'git') return 'git';
  if (rest.length === 1 && rest[0] === 'agents') return 'agents';
  return null;
}

export function workspaceTabHref(workspaceId: string, tab: WorkspaceOverviewTab): Href {
  const root = `/(main)/workspace/${workspaceId}`;
  if (tab === 'apps') return root as Href;
  return `${root}/${tab}` as Href;
}
