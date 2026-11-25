import assert from 'node:assert/strict';
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';

import { parseArgs } from '../pwa/flows/args.mjs';
import { interpolate } from '../pwa/flows/interpolate.mjs';
import { locatorPlan, visibleTextLocator } from '../pwa/flows/locate.mjs';
import { waitForVisible } from '../pwa/flows/visible.mjs';
import { emptyReport, finishReport, relativeScreenshot, screenshotName, summaryReport, writeJson } from '../pwa/flows/report.mjs';
import { scriptedAgentEnvironment } from '../pwa/flows/host.mjs';

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
    workspaceName: 'Agent Up',
  });

  assert.equal(flow.value, 'http://127.0.0.1:5100');
  assert.equal(flow.target.name, 'Open workspace Agent Up');
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

test('the Mobile runner scrolls Visible matches into view instead of waiting off-screen', async () => {
  const source = await readFile(new URL('../pwa/flows/run.mjs', import.meta.url), 'utf8');
  assert.match(source, /import \{ waitForVisible \} from '\.\/visible\.mjs'/);
  assert.match(source, /await waitForVisible\(locator\)/);
  assert.match(source, /await locator\.scrollIntoViewIfNeeded\(\{ timeout: 180_000 \}\)/);
});

test('Visible waits attach, scroll the match into view, then wait until it is on screen', async () => {
  const calls = [];
  const locator = {
    async waitFor(options) { calls.push(['waitFor', options]); },
    async scrollIntoViewIfNeeded(options) { calls.push(['scrollIntoViewIfNeeded', options]); },
  };

  await waitForVisible(locator, { timeout: 12_000 });

  assert.deepEqual(calls, [
    ['waitFor', { state: 'attached', timeout: 12_000 }],
    ['scrollIntoViewIfNeeded', { timeout: 12_000 }],
    ['waitFor', { state: 'visible', timeout: 12_000 }],
  ]);
});

test('Visible waits keep scrolling after the first match detaches', async () => {
  let attempts = 0;
  const locator = {
    async waitFor() {},
    async scrollIntoViewIfNeeded() {
      attempts += 1;
      if (attempts === 1) throw new Error('Element is not attached to the DOM');
    },
  };

  await waitForVisible(locator, { timeout: 2_000 });
  assert.equal(attempts, 2);
});

test('free-text expectations wait on a visible match, not on a hidden earlier screen', () => {
  const calls = [];
  const locator = {
    filter(options) { calls.push(['filter', options]); return this; },
    first() { calls.push(['first']); return this; },
  };
  const page = { getByText(text, options) { calls.push(['getByText', text, options]); return locator; } };

  assert.equal(visibleTextLocator(page, 'Databases'), locator);
  assert.deepEqual(calls, [
    ['getByText', 'Databases', { exact: false }],
    ['filter', { visible: true }],
    ['first'],
  ]);
});

test('report.json names each step screenshot and the overall verdict', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'mobile-validation-'));
  try {
    const started = '2026-10-24T00:00:00.000Z';
    const report = emptyReport({ flowId: 'connect', startedAtUtc: started });
    report.steps.push({
      id: 'try-and-save',
      screenshot: relativeScreenshot(1, 'try-and-save'),
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
    assert.equal(saved.flows[0].steps[0].screenshot, '01-try-and-save.png');
    assert.equal(screenshotName(0, 'initial'), '00-initial.png');
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('scriptedAgentEnvironment writes an executable shim onto PATH', async () => {
  const repo = await mkdtemp(join(tmpdir(), 'scripted-agent-'));
  try {
    const agents = join(repo, 'AgentUp.TestAgents', 'bin', 'Debug', 'net10.0');
    await mkdir(agents, { recursive: true });
    const executable = join(agents, 'AgentUp.TestAgents');
    await writeFile(executable, '#!/bin/sh\n');
    await chmod(executable, 0o755);
    const bin = join(repo, 'bin');
    const env = await scriptedAgentEnvironment(repo, bin);
    const shim = env['Agents__test-agent-scripted__Command'];
    assert.equal((await readFile(shim, 'utf8')).includes(executable), true);
    assert.equal(env.PATH.startsWith(bin), true);
  } finally {
    await rm(repo, { recursive: true, force: true });
  }
});
