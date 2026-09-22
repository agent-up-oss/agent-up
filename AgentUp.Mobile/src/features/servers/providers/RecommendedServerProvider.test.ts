import assert from 'node:assert/strict';
import test from 'node:test';
import { cloudServer, defaultCloudDisplayName, listSavedServers, listServers, readRecommendedServer } from './RecommendedServerProvider';
import { hasSavedSignIn } from '../models/ConfiguredServer';
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

test('listServers includes Demo when no Cloud URL is configured', () => {
  const listed = listServers({ servers: [], activeServerId: null }, null);
  assert.equal(listed.length, 1);
  assert.equal(listed[0].isFake, true);
  assert.equal(listed[0].displayName, 'Demo');
  assert.equal(listed[0].canRemove, false);
});
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
  assert.equal(listed[0].isFake, true);
  assert.equal(listed[0].displayName, 'Demo');
  assert.equal(listed[1].isRecommended, true);
  assert.equal(listed[1].displayName, 'Agent-Up Cloud');
  assert.equal(listed[2].url, 'http://127.0.0.1:5100');
});

test('listServers includes Demo when no Cloud URL is configured', () => {
  const listed = listServers({ servers: [], activeServerId: null }, null);
  assert.equal(listed.length, 1);
  assert.equal(listed[0].isFake, true);
  assert.equal(listed[0].displayName, 'Demo');
  assert.equal(listed[0].canRemove, false);
});

test('cloudServer keeps persisted open access', () => {
  const cloud = cloudServer(
    {
      servers: [{ id: 'saved-cloud', url: 'http://127.0.0.1:5288', openAccess: true }],
      activeServerId: 'saved-cloud',
    },
    { id: 'recommended', url: 'http://127.0.0.1:5288', displayName: 'Agent-Up Cloud' },
  );
  assert.equal(cloud?.openAccess, true);
  assert.equal(hasSavedSignIn(cloud), true);
});

test('hasSavedSignIn treats a token or open access as signed in', () => {
  assert.equal(hasSavedSignIn({ accessToken: 'token' }), true);
  assert.equal(hasSavedSignIn({ openAccess: true }), true);
  assert.equal(hasSavedSignIn({ accessToken: 'token', openAccess: false }), true);
  assert.equal(hasSavedSignIn({}), false);
  assert.equal(hasSavedSignIn(null), false);
});
