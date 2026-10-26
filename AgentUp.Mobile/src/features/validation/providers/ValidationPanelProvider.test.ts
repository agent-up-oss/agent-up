import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { ValidationFlow } from '../models/ValidationFlow';
import {
  describeAssertion,
  idleStageViews,
  showsValidationChrome,
  adjacentFlowIndex,
  swipeFlowDelta,
  compactValidationCopy,
  selectedFlowSubstep,
  validationMenuLabel,
  validationRunGlyph,
  validationStages,
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

test('menu copy names show and hide the scenario list', () => {
  assert.equal(validationMenuLabel(false), 'Show validation scenarios');
  assert.equal(validationMenuLabel(true), 'Hide validation scenarios');
});

test('adjacent flow index wraps at both ends', () => {
  assert.equal(adjacentFlowIndex(0, 3, -1), 2);
  assert.equal(adjacentFlowIndex(2, 3, 1), 0);
  assert.equal(adjacentFlowIndex(1, 3, 1), 2);
  assert.equal(adjacentFlowIndex(0, 0, 1), 0);
});

test('swipe delta ignores short moves and maps up to next', () => {
  assert.equal(swipeFlowDelta(0), 0);
  assert.equal(swipeFlowDelta(23), 0);
  assert.equal(swipeFlowDelta(-24), 1);
  assert.equal(swipeFlowDelta(24), -1);
});

test('compact copy shows the flow name and only the current substep', () => {
  const idle = idleStageViews(flow);
  assert.deepEqual(
    compactValidationCopy({ name: flow.name, stages: idle, resultMessage: null }),
    { title: 'Checkout featured goods', substep: 'Starting page' },
  );
  const running = idle.map((stage, index) => (
    index === 1
      ? { ...stage, state: 'running' as const, checks: [{ ...stage.checks[0], state: 'running' as const }] }
      : stage
  ));
  assert.equal(selectedFlowSubstep(running, null), 'Text contains "Harbor Mug × 1"');
  assert.equal(selectedFlowSubstep(idle, 'Validation passed.'), 'Validation passed.');
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

test('idle stages start pending with glyphs for running, passed, and failed', () => {
  const stages = idleStageViews(flow);
  assert.equal(stages[0].state, 'pending');
  assert.equal(stages[0].checks[0].state, 'pending');
  assert.equal(validationRunGlyph('pending'), '○');
  assert.equal(validationRunGlyph('running'), '●');
  assert.equal(validationRunGlyph('passed'), '✓');
  assert.equal(validationRunGlyph('failed'), '✗');
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
