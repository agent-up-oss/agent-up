export const PublicIconNames = ['apps', 'git', 'agents', 'settings', 'download', 'menu'] as const;

export type PublicIconName = (typeof PublicIconNames)[number];

const files: Record<PublicIconName, string> = {
  apps: '/icons/apps.svg',
  git: '/icons/git.svg',
  agents: '/icons/agents.svg',
  settings: '/icons/settings.svg',
  download: '/icons/download.svg',
  menu: '/icons/menu.svg',
};

/** Root-relative PWA path for a self-hosted product icon. */
export function publicIconUri(name: PublicIconName): string {
  return files[name];
}
