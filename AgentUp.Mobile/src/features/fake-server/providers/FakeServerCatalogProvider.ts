import type { ConfiguredServer } from '@/features/servers/models/ConfiguredServer';
import type { ServerSelection } from '@/features/servers/providers/ServerStorageProvider';
import { fakeServerDisplayName, fakeServerId, fakeServerUrl, isFakeServerId, isFakeServerUrl } from '../models/FakeServerIdentity';

export function fakeServerEntry(selection: ServerSelection): ConfiguredServer {
  const saved = selection.servers.find(server => isFakeServerId(server.id) || isFakeServerUrl(server.url));
  return {
    id: fakeServerId,
    url: fakeServerUrl,
    displayName: fakeServerDisplayName,
    accessToken: saved?.accessToken,
    openAccess: saved?.openAccess,
    canRemove: false,
    isFake: true,
  };
}

export function upsertFakeServer(selection: ServerSelection): ServerSelection {
  const withoutFake = selection.servers.filter(server => !isFakeServerId(server.id) && !isFakeServerUrl(server.url));
  const server: ConfiguredServer = {
    id: fakeServerId,
    url: fakeServerUrl,
    displayName: fakeServerDisplayName,
    openAccess: true,
    canRemove: false,
    isFake: true,
  };
  return { servers: [server, ...withoutFake], activeServerId: fakeServerId };
}

export function isFakeSelection(selection: ServerSelection): boolean {
  if (isFakeServerId(selection.activeServerId)) return true;
  const active = selection.servers.find(server => server.id === selection.activeServerId);
  return !!active && isFakeServerUrl(active.url);
}
