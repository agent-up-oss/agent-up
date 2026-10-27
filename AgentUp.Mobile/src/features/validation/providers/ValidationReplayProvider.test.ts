import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { ValidationFlow } from '../models/ValidationFlow';
import { parseScriptError, runValidationReplay, type ValidationReplayHost, type ValidationReplayReporter } from './ValidationReplayProvider';

const flow: ValidationFlow = {
  id: 'checkout-featured-goods',
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
      target: { text: 'Add to cart', selector: 'button[data-add="mug"]' },
      expectations: [{ kind: 'Text', value: 'Harbor Mug × 1' }],
    },
    {
      id: 'place-order',
      description: 'Place the order',
      action: 'Click',
      target: { name: 'Place order', selector: '#place' },
      expectations: [{ kind: 'Text', value: 'Order 1009' }],
    },
  ],
  version: 1,
  surface: 'Application',
};

function reporter(): ValidationReplayReporter & { events: string[] } {
  const events: string[] = [];
  return {
    events,
    beginFlow: () => events.push('beginFlow'),
    beginStage: id => events.push(`beginStage:${id}`),
    beginCheck: (id, index) => events.push(`beginCheck:${id}:${index}`),
    completeCheck: (id, index, passed) => events.push(`completeCheck:${id}:${index}:${passed}`),
    completeStage: (id, passed) => events.push(`completeStage:${id}:${passed}`),
    completeFlow: (passed, message) => events.push(`completeFlow:${passed}:${message ?? ''}`),
  };
}

function host(overrides: Partial<{ title: string; texts: string[]; clickError: string | null }> = {}): ValidationReplayHost & { scripts: string[] } {
  const scripts: string[] = [];
  const texts = new Set(overrides.texts ?? ['Harbor Mug', 'Harbor Mug × 1', 'Order 1009']);
  return {
    scripts,
    async eval(script: string) {
      scripts.push(script);
      if (script.includes('document.readyState')) return 'complete';
      if (script.includes('document.title')) return overrides.title ?? 'Harbor Shop';
      if (script.includes('innerText')) {
        const encoded = /includes\((.*)\)/.exec(script)?.[1];
        const needle = encoded ? JSON.parse(encoded) as string : '';
        return texts.has(needle) ? 'true' : 'false';
      }
      if (script.includes('.click()') && overrides.clickError) return JSON.stringify({ error: overrides.clickError });
      if (script.trimStart().startsWith('{')) return script;
      return JSON.stringify({ ok: true });
    },
    async delay() {},
  };
}

test('runValidationReplay stages clicks then ticks every check on a passing Demo checkout', async () => {
  const record = reporter();
  const page = host();
  const result = await runValidationReplay({
    flow,
    host: page,
    reporter: record,
    skipCosmeticStaging: true,
  });

  assert.equal(result.succeeded, true);
  assert.equal(result.message.includes('passed'), true);
  assert.equal(record.events.includes('beginStage:initial'), true);
  assert.equal(record.events.includes('completeCheck:place-order:0:true'), true);
  assert.equal(record.events.at(-1)?.startsWith('completeFlow:true:'), true);
  assert.equal(page.scripts.some(script => script.includes('.click()')), true);
});

test('runValidationReplay moves the cursor and pings before a click when cosmetics are on', async () => {
  const record = reporter();
  const page = host();
  await runValidationReplay({ flow, host: page, reporter: record, skipCosmeticStaging: false });
  assert.equal(page.scripts.some(script => script.includes('__agentUpMouse')), true);
  assert.equal(page.scripts.some(script => script.includes('__agentUpClickRing')), true);
  assert.equal(page.scripts.some(script =>
    script.includes("getElementById('__agentUpMouse')") && script.includes('m.remove()')
  ), true);
});

test('runValidationReplay fails the starting page when the title never matches', async () => {
  const record = reporter();
  const result = await runValidationReplay({
    flow,
    host: host({ title: 'Wrong' }),
    reporter: record,
    skipCosmeticStaging: true,
  });
  assert.equal(result.succeeded, false);
  assert.equal(result.message.includes('title'), true);
  assert.equal(record.events.includes('completeStage:add-mug:true'), false);
});

test('parseScriptError reads injected browser errors and ignores ok payloads', () => {
  assert.equal(parseScriptError(null), 'Browser script returned no result.');
  assert.equal(parseScriptError(JSON.stringify({ error: 'Element not found' })), 'Element not found');
  assert.equal(parseScriptError(JSON.stringify({ ok: true })), null);
  assert.equal(parseScriptError('true'), null);
});
