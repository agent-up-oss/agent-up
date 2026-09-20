import assert from 'node:assert/strict';
import test from 'node:test';
import { isFeatureAvailable, presentEntitlements } from '../models/Entitlements';
import { getEntitlements } from './EntitlementsApiProvider';

test('getEntitlements reads the permission document', async () => {
  const document = await getEntitlements({ url: 'http://localhost:5000', accessToken: 't' }, (async () =>
    new Response(JSON.stringify({
      displayName: 'Self-hosted',
      billing: 'free',
      features: { 'agent.prompt': { available: true }, 'git.write': { available: false } },
      limits: {},
    }))) as typeof fetch);

  assert.equal(document?.displayName, 'Self-hosted');
  assert.equal(presentEntitlements(document).summary, '1 of 2 operations available');
});

test('presentEntitlements does not unlock when the document is missing', () => {
  const card = presentEntitlements(null);
  assert.equal(card.available, false);
  assert.equal(card.displayName, 'Edition unavailable');
});

test('isFeatureAvailable follows the permission document', () => {
  assert.equal(isFeatureAvailable({
    apiVersion: '1', connectionId: 'c', subject: 'u', source: 'directory', edition: 'pro',
    displayName: 'Pro', billing: 'subscription', revision: '1',
    features: { 'workspace.create': { available: false }, 'agent.prompt': { available: true } },
    limits: {},
  }, 'workspace.create'), false);
  assert.equal(isFeatureAvailable(null, 'workspace.create'), false);
});

test('getEntitlements returns null when unauthorized', async () => {
  const document = await getEntitlements({ url: 'http://localhost:5000', accessToken: 't' }, (async () =>
    new Response('{}', { status: 401 })) as typeof fetch);
  assert.equal(document, null);
});
