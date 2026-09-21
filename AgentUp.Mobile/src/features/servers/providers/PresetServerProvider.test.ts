import assert from 'node:assert/strict';
import test from 'node:test';
import { resolvePresetServerUrl, workspaceHref } from './PresetServerProvider';

test('maps the Cloud URL onto the Cloud row', () => {
  assert.deepEqual(
    resolvePresetServerUrl('http://localhost:5288/', 'http://localhost:5288'),
    { kind: 'cloud', url: 'http://localhost:5288' },
  );
});

test('keeps any other URL as a self-hosted preset', () => {
  assert.deepEqual(
    resolvePresetServerUrl('http://127.0.0.1:5001/', 'http://localhost:5288'),
    { kind: 'selfHosted', url: 'http://127.0.0.1:5001' },
  );
});

test('deep-links a Cloud workspace after connect', () => {
  assert.equal(workspaceHref('ws-a'), '/(main)/workspace/ws-a');
  assert.equal(workspaceHref(), '/(main)/workspace');
});
