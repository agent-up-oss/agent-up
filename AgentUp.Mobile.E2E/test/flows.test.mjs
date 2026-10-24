import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';

import { parseArgs } from '../pwa/flows/args.mjs';
import { interpolate } from '../pwa/flows/interpolate.mjs';
import { locatorPlan } from '../pwa/flows/locate.mjs';
import { emptyReport, finishReport, relativeScreenshot, summaryReport, writeJson } from '../pwa/flows/report.mjs';

test('parseArgs requires --out and accepts --flow', () => {
  assert.deepEqual(parseArgs(['--out', '/tmp/out', '--flow', 'git']), { outDir: '/tmp/out', flowId: 'git' });
  assert.throws(() => parseArgs([]), /--out is required/);
  assert.throws(() => parseArgs(['--flow', 'git']), /--out is required/);
});

test('interpolate replaces host placeholders throughout a flow', () => {
  const flow = interpolate({
    value: '{serverUrl}',
    target: { name: 'Open workspace {workspaceName}' },
    expectations: [{ kind: 'Url', value: '/workspace/{workspaceId}/git' }],
  }, {
    serverUrl: 'http://127.0.0.1:5100',
    workspaceId: 'ws-1',
    workspaceName: 'Agent-Up',
  });

  assert.equal(flow.value, 'http://127.0.0.1:5100');
  assert.equal(flow.target.name, 'Open workspace Agent-Up');
  assert.equal(flow.expectations[0].value, '/workspace/ws-1/git');
});

test('interpolate refuses an unknown placeholder', () => {
  assert.throws(() => interpolate('{nope}', { serverUrl: 'http://127.0.0.1:1' }), /Unknown placeholder \{nope\}/);
});

test('locatorPlan prefers role, then label, then text, and rejects coordinates', () => {
  assert.deepEqual(
    locatorPlan({ role: 'button', name: 'Try and save' }),
    { method: 'getByRole', role: 'button', name: 'Try and save', exact: true },
  );
  assert.deepEqual(
    locatorPlan({ label: 'Server URL' }),
    { method: 'getByLabel', label: 'Server URL', exact: true },
  );
  assert.deepEqual(
    locatorPlan({ text: 'Changes' }),
    { method: 'getByText', text: 'Changes', exact: true },
  );
  assert.throws(() => locatorPlan({ selector: '#x' }), /reject Selector, X, and Y/);
  assert.throws(() => locatorPlan({ x: 10, y: 20 }), /reject Selector, X, and Y/);
});

test('report.json names each step screenshot and the overall verdict', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'mobile-validation-'));
  try {
    const started = '2026-10-24T00:00:00.000Z';
    const report = emptyReport({ flowId: 'connect', startedAtUtc: started });
    report.steps.push({
      id: 'try-and-save',
      screenshot: relativeScreenshot('connect', 'try-and-save'),
      passed: true,
    });
    const finished = finishReport(report, { passed: true, finishedAtUtc: '2026-10-24T00:00:01.000Z' });
    const summary = summaryReport({
      startedAtUtc: started,
      finishedAtUtc: finished.finishedAtUtc,
      flows: [finished],
    });
    const path = join(dir, 'report.json');
    await writeJson(path, summary);
    const saved = JSON.parse(await readFile(path, 'utf8'));
    assert.equal(saved.surface, 'Mobile');
    assert.equal(saved.passed, true);
    assert.equal(saved.flows[0].steps[0].screenshot, 'connect/try-and-save.png');
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});
