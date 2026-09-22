import { fakeServerEntry } from '../providers/FakeServerCatalogProvider';
import { loadFakeServerDefinition } from '../providers/FakeServerDefinitionProvider';
import { installFakeServerFetch, uninstallFakeServerFetch } from '../providers/FakeServerFetchProvider';
import { FakeBackendService } from '../services/FakeBackendService';

const backend = new FakeBackendService(loadFakeServerDefinition());

export class FakeServerController {
  catalog(activeServerId?: string | null) {
    return fakeServerEntry({
      servers: [],
      activeServerId: activeServerId ?? null,
    });
  }

  matches(url: string | null | undefined) {
    return backend.matches(url);
  }

  applicationHtml(allocatedPort: number) {
    return backend.applicationHtml(allocatedPort);
  }

  activate() {
    backend.reset();
    installFakeServerFetch(backend);
  }

  deactivate() {
    uninstallFakeServerFetch();
  }

  reset() {
    backend.reset();
  }
}

export const fakeServers = new FakeServerController();
