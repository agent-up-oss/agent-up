import assert from 'node:assert/strict';
import test from 'node:test';

import { connectStep, connectStepPending } from '../harness/connectReady.mjs';

test('a visible picker means the chat already connected', () => {
  assert.equal(connectStep({ pickerVisible: true, formVisible: true, promptVisible: true }), 'connected');
});

test('the connect form is the fallback when the launch URL missed', () => {
  assert.equal(connectStep({ pickerVisible: false, formVisible: true, promptVisible: false }), 'fill-form');
});

test('the picker prompt without buttons means getAgent has not listed agents yet', () => {
  assert.equal(connectStep({ pickerVisible: false, formVisible: false, promptVisible: true }), 'wait-agents');
});

test('nothing from the harness means the launch has to be delivered again', () => {
  assert.equal(connectStep({ pickerVisible: false, formVisible: false, promptVisible: false }), 'relaunch');
});

test('a blank frame or a picker prompt keeps waiting until the deadline', () => {
  assert.equal(connectStepPending('relaunch'), true);
  assert.equal(connectStepPending('wait-agents'), true);
  assert.equal(connectStepPending('connected'), false);
  assert.equal(connectStepPending('fill-form'), false);
});

test('Detox connect helpers take connectStep as an argument rather than closing over harness', async () => {
  const { readFile } = await import('node:fs/promises');
  const source = await readFile(new URL('../detox/signIn.test.js', import.meta.url), 'utf8');
  const helpers = source.slice(source.indexOf('function pickerFor'));
  assert.equal(
    helpers.includes('harness.'),
    false,
    'waitForConnectStep lives outside describe, so a harness. lookup is a ReferenceError on every native scenario.',
  );
});
