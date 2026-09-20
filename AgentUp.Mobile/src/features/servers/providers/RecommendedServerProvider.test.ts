import assert from 'node:assert/strict';
import test from 'node:test';
import { cloudServer, defaultCloudDisplayName, listSavedServers, listServers, readRecommendedServer } from './RecommendedServerProvider';
import type { ServerSelection } from './ServerStorageProvider';

test('readRecommendedServer returns null when no URL is configured', () => {
  assert.equal(readRecommendedServer({}), null);
});

test('readRecommendedServer defaults the Cloud display name', () => {
  const recommended = readRecommendedServer({
    EXPO_PUBLIC_RECOMMENDED_SERVER_URL: 'http://127.0.0.1:5288/',
  });
  assert.equal(recommended?.url, 'http://127.0.0.1:5288');
  assert.equal(recommended?.displayName, defaultCloudDisplayName);
});

test('listSavedServers omits the Cloud URL', () => {
  const selection: ServerSelection = {
    servers: [
      { id: 'self', url: 'http://127.0.0.1:5100' },
      { id: 'saved-cloud', url: 'http://127.0.0.1:5288', accessToken: 'token' },
    ],
    activeServerId: 'self',
  };
  const recommended = {
    id: 'recommended' as const,
    url: 'http://127.0.0.1:5288',
    displayName: 'Agent-Up Cloud',
  };
  const saved = listSavedServers(selection, recommended);
  const cloud = cloudServer(selection, recommended);

  assert.equal(saved.length, 1);
  assert.equal(saved[0].url, 'http://127.0.0.1:5100');
  assert.equal(cloud?.id, 'saved-cloud');
  assert.equal(cloud?.displayName, 'Agent-Up Cloud');
  assert.equal(cloud?.accessToken, 'token');
  assert.equal(cloud?.canRemove, false);
});

test('listServers puts Cloud ahead of saved servers', () => {
  const listed = listServers(
    {
      servers: [
        { id: 'self', url: 'http://127.0.0.1:5100' },
        { id: 'saved-cloud', url: 'http://127.0.0.1:5288' },
      ],
      activeServerId: 'self',
    },
    { id: 'recommended', url: 'http://127.0.0.1:5288', displayName: 'Agent-Up Cloud' },
  );
  assert.equal(listed[0].isRecommended, true);
  assert.equal(listed[0].displayName, 'Agent-Up Cloud');
  assert.equal(listed[1].url, 'http://127.0.0.1:5100');
});
