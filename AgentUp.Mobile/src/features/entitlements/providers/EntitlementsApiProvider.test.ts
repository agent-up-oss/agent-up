import assert from 'node:assert/strict';
import test from 'node:test';
import { isFeatureAvailable, presentPlanCard, workspaceCreateFeature } from '../models/Entitlements';
import { getEntitlements } from './EntitlementsApiProvider';

const community = {
  apiVersion: '1', connectionId: 'local', subject: 'admin', source: 'selfHosted', edition: 'community',
  displayName: 'Community', billing: 'free', revision: '1',
  features: { 'workspace.create': { available: true }, 'agent.prompt': { available: true } },
  limits: {},
};

const restricted = {
  apiVersion: '1', connectionId: 'cloud', subject: 'user', source: 'directory', edition: 'team',
  displayName: 'Team', billing: 'subscription', revision: '9',
  features: { 'workspace.create': { available: false }, 'agent.prompt': { available: true } },
  limits: { 'workspace.count': { max: 3, used: 2 } },
};

test('getEntitlements reads the permission document', async () => {
  const document = await getEntitlements({ url: 'http://localhost:5000', accessToken: 't' }, (async () =>
    new Response(JSON.stringify({
      displayName: 'Self-hosted',
      billing: 'free',
      features: { 'agent.prompt': { available: true }, 'git.write': { available: false } },
      limits: {},
    }))) as typeof fetch);

  assert.equal(document?.displayName, 'Self-hosted');
  assert.equal(presentPlanCard(document).summary, '1 of 2 operations available');
});

test('presentPlanCard renders a community document from features and limits', () => {
  const card = presentPlanCard(community);
  assert.equal(card.displayName, 'Community');
  assert.equal(card.billing, 'free');
  assert.equal(card.features[0]?.id, 'workspace.create');
  assert.equal(card.limits.length, 0);
});

test('presentPlanCard renders a non-community document without edition-name branching', () => {
  const card = presentPlanCard(restricted);
  assert.equal(card.displayName, 'Team');
  assert.equal(card.summary, '1 of 2 operations available');
  assert.equal(card.limits[0]?.id, 'workspace.count');
  assert.equal(card.limits[0]?.used, 2);
});

test('presentPlanCard does not unlock when the document is missing', () => {
  const card = presentPlanCard(null);
  assert.equal(card.available, false);
  assert.equal(card.displayName, 'Plan unavailable');
});

test('isFeatureAvailable follows the permission document', () => {
  assert.equal(isFeatureAvailable(restricted, workspaceCreateFeature), false);
  assert.equal(isFeatureAvailable(null, workspaceCreateFeature), false);
});

test('getEntitlements returns null when unauthorized', async () => {
  const document = await getEntitlements({ url: 'http://localhost:5000', accessToken: 't' }, (async () =>
    new Response('{}', { status: 401 })) as typeof fetch);
  assert.equal(document, null);
});

test('getEntitlements returns null when features are not a document', async () => {
  const document = await getEntitlements({ url: 'http://localhost:5000', accessToken: 't' }, (async () =>
    new Response(JSON.stringify({ displayName: 'Broken', features: [null] }))) as typeof fetch);
  assert.equal(document, null);
});
