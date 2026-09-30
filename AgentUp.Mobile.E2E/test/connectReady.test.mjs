import assert from 'node:assert/strict';
import test from 'node:test';

import { connectStep } from '../harness/connectReady.mjs';

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
