import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  bodyKind,
  compare,
  completeness,
  concrete,
  expectedDemo,
  keysOf,
  type ParityAnswer,
  type ParityEntry,
  type ParityManifest,
} from '../../../../../AgentUp.FakeServer/parity.mjs';
import { loadFakeServerDefinition } from '../providers/FakeServerDefinitionProvider';
import { FakeBackendService } from './FakeBackendService';

// Mobile's Demo held to the client contract the real Server and Desktop's Demo answer too. The
// Backend · Demo (Mobile) leg of CI runs this file; the manifest says why each difference exists.

const root = new URL('../../../../../', import.meta.url);
const manifest = JSON.parse(readFileSync(new URL('AgentUp.FakeServer/parity.json', root), 'utf8')) as ParityManifest;
const openapi = JSON.parse(readFileSync(new URL('docs/api-reference/openapi.json', root), 'utf8'));

/** One request against a Demo of its own, so a write never changes what a later read sees. */
function answer(entry: ParityEntry): ParityAnswer {
  const method = entry.route.slice(0, entry.route.indexOf(' '));
  const { path, query } = concrete(entry, manifest.fixtures.demo);
  const response = new FakeBackendService(loadFakeServerDefinition(), () => () => {})
    .handle({ method, path, query: method === 'GET' ? query : '', body: null });
  const text = typeof response.body === 'string' ? response.body : response.body == null ? '' : JSON.stringify(response.body);
  const body = response.contentType?.includes('text/event-stream') ? 'stream' : bodyKind(response.contentType, text);
  return { status: response.status, body, keys: keysOf(body, text) };
}

const absent = (entry: ParityEntry) => typeof entry.demo === 'object' && 'absent' in entry.demo;

test('the parity manifest classifies every operation in the API reference', () => {
  assert.deepEqual(completeness(manifest, openapi), []);
});

test('the Demo answers every read the manifest says it shares, as recorded', () => {
  const problems = manifest.operations
    .filter(entry => entry.route.startsWith('GET ') && !absent(entry))
    .flatMap(entry => compare(entry.route, expectedDemo(entry)!, answer(entry)));
  assert.deepEqual(problems, []);
});

test('the Demo answers nothing the manifest says only a real Server answers', () => {
  const answered = manifest.operations
    .filter(absent)
    .map(entry => ({ route: entry.route, status: answer(entry).status }))
    .filter(result => result.status !== 404)
    .map(result => `${result.route} answered ${result.status}; mark it as a Demo answer in parity.json.`);
  assert.deepEqual(answered, []);
});
