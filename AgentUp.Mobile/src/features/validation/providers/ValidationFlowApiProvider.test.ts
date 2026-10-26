import assert from 'node:assert/strict';
import { test } from 'node:test';
import { getValidationFlow, listValidationFlows } from './ValidationFlowApiProvider';

type Recorded = { url: string; init: RequestInit };

function fakeFetch(status: number, body: string, recorded: Recorded[]): typeof fetch {
  return (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    return new Response(body, { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

const flow = {
  id: 'checkout-featured-goods',
  workspaceId: 'harbor-shop',
  application: 'Storefront',
  name: 'Checkout featured goods',
  description: 'Featured products are listed.',
  initialPath: '/',
  initialExpectations: [{ kind: 'Title', value: 'Harbor Shop' }],
  steps: [],
  version: 1,
  surface: 'Application',
};

test('listValidationFlows requests the application query on the workspace route', async () => {
  const recorded: Recorded[] = [];
  const flows = await listValidationFlows(
    { url: 'http://localhost:5000' },
    'ws 1',
    'Storefront',
    fakeFetch(200, JSON.stringify([flow]), recorded),
  );

  assert.equal(flows[0].id, 'checkout-featured-goods');
  assert.equal(
    recorded[0].url,
    'http://localhost:5000/api/workspaces/ws%201/validation-flows?application=Storefront',
  );
});

test('listValidationFlows returns an empty list when the Server has no checks', async () => {
  const flows = await listValidationFlows(
    { url: 'http://localhost:5000' },
    'ws',
    'Docs',
    fakeFetch(200, '[]', []),
  );

  assert.deepEqual(flows, []);
});

test('getValidationFlow requests the flow by id', async () => {
  const recorded: Recorded[] = [];
  const found = await getValidationFlow(
    { url: 'http://localhost:5000' },
    'harbor-shop',
    'checkout-featured-goods',
    fakeFetch(200, JSON.stringify(flow), recorded),
  );

  assert.equal(found?.name, 'Checkout featured goods');
  assert.equal(
    recorded[0].url,
    'http://localhost:5000/api/workspaces/harbor-shop/validation-flows/checkout-featured-goods',
  );
});

test('getValidationFlow returns null when the flow is missing', async () => {
  const found = await getValidationFlow(
    { url: 'http://localhost:5000' },
    'harbor-shop',
    'missing',
    fakeFetch(404, '', []),
  );

  assert.equal(found, null);
});
