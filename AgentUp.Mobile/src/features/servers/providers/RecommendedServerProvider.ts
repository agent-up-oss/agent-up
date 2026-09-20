import type { ConfiguredServer } from '../models/ConfiguredServer';
import { normalizeServerUrl } from './ServerUrlProvider';
import type { ServerSelection } from './ServerStorageProvider';

export const recommendedServerId = 'recommended';
export const defaultCloudDisplayName = 'Agent-Up Cloud';

export type RecommendedServer = {
  id: typeof recommendedServerId;
  url: string;
  displayName: string;
};

export function readRecommendedServer(env: Record<string, string | undefined> = process.env): RecommendedServer | null {
  const url = env.EXPO_PUBLIC_RECOMMENDED_SERVER_URL ?? env.AGENTUP_RECOMMENDED_SERVER_URL;
  if (!url?.trim()) return null;

  try {
    const name = env.EXPO_PUBLIC_RECOMMENDED_SERVER_NAME ?? env.AGENTUP_RECOMMENDED_SERVER_NAME;
    return {
      id: recommendedServerId,
      url: normalizeServerUrl(url),
      displayName: name?.trim() || defaultCloudDisplayName,
    };
  } catch {
    return null;
  }
}

export function cloudServer(selection: ServerSelection, recommended: RecommendedServer | null): ConfiguredServer | null {
  if (!recommended) return null;
  const saved = selection.servers.find(server => server.url === recommended.url);
  return {
    id: saved?.id ?? recommended.id,
    url: recommended.url,
    displayName: recommended.displayName,
    accessToken: saved?.accessToken,
    openAccess: saved?.openAccess,
    isRecommended: true,
    canRemove: false,
  };
}

export function listSavedServers(selection: ServerSelection, recommended: RecommendedServer | null): ConfiguredServer[] {
  return selection.servers
    .filter(server => server.url !== recommended?.url)
    .map(server => ({
      ...server,
      displayName: server.displayName ?? server.url,
      isRecommended: false,
      canRemove: true,
    }));
}

export function listServers(selection: ServerSelection, recommended: RecommendedServer | null): ConfiguredServer[] {
  const cloud = cloudServer(selection, recommended);
  const saved = listSavedServers(selection, recommended);
  return cloud ? [cloud, ...saved] : saved;
}
