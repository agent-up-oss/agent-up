import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { ValidationFlow } from '../models/ValidationFlow';
import {
  describeAssertion,
  showsValidationChrome,
  validationStages,
  validationToggleLabel,
} from './ValidationPanelProvider';

const flow: ValidationFlow = {
  id: 'checkout',
  workspaceId: 'harbor-shop',
  application: 'Storefront',
  name: 'Checkout featured goods',
  description: 'Featured products are listed.',
  initialPath: '/',
  initialExpectations: [
    { kind: 'Title', value: 'Harbor Shop' },
    { kind: 'Text', value: 'Harbor Mug' },
  ],
  steps: [
    {
      id: 'add-mug',
      description: 'Add Harbor Mug to the cart',
      action: 'Click',
      expectations: [{ kind: 'Text', value: 'Harbor Mug × 1' }],
    },
  ],
  version: 1,
};

test('shows validation on HTTP process apps and hides it for desktop and database apps', () => {
  assert.equal(showsValidationChrome({ name: 'Storefront', state: 'Running', kind: 'Process' }), true);
  assert.equal(showsValidationChrome({ name: 'Storefront', state: 'Running', kind: 'Process', database: false }), true);
  assert.equal(showsValidationChrome({ name: 'Sample Desktop', state: 'Running', kind: 'Desktop' }), false);
  assert.equal(showsValidationChrome({ name: 'Inventory', state: 'Running', kind: 'Process', database: true }), false);
  assert.equal(showsValidationChrome(undefined), false);
});

test('toggle copy names open and close', () => {
  assert.equal(validationToggleLabel(false), 'Open validation');
  assert.equal(validationToggleLabel(true), 'Close validation');
});

test('stages start with the initial page then one stage per step', () => {
  const stages = validationStages(flow);
  assert.equal(stages[0].id, 'initial');
  assert.equal(stages[0].title, 'Starting page');
  assert.deepEqual(stages[0].checks, ['Title is "Harbor Shop"', 'Text contains "Harbor Mug"']);
  assert.equal(stages[1].id, 'add-mug');
  assert.equal(stages[1].title, 'Add Harbor Mug to the cart');
  assert.deepEqual(stages[1].checks, ['Text contains "Harbor Mug × 1"']);
});

test('assertion copy covers visible, enabled, and named targets', () => {
  assert.equal(describeAssertion({ kind: 'Url', value: '/' }), 'URL is "/"');
  assert.equal(
    describeAssertion({ kind: 'Visible', value: 'true', target: { name: 'Place order' } }),
    'Place order is visible',
  );
  assert.equal(
    describeAssertion({ kind: 'Enabled', value: 'false', target: { label: 'Run' } }),
    'Run is disabled',
  );
  assert.equal(describeAssertion({ kind: 'Absent', value: 'true', target: { text: 'Error' } }), 'Error is absent');
});
