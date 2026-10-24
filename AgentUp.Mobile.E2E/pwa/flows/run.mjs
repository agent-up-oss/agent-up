import { mkdir, readdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { chromium, devices } from '@playwright/test';

import { parseArgs } from './args.mjs';
import { startValidationHost, varsFor } from './host.mjs';
import { interpolate } from './interpolate.mjs';
import { locatorFor } from './locate.mjs';
import {
  emptyReport,
  finishReport,
  relativeScreenshot,
  screenshotPath,
  summaryReport,
  writeJson,
} from './report.mjs';
import { waitFor } from '../../harness/wait.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));

const argv = process.argv.slice(2);
const { outDir, flowId } = parseArgs(argv);
const repoRoot = process.env.AGENTUP_VALIDATE_REPO
  ?? join(HERE, '..', '..', '..');

const startedAtUtc = new Date().toISOString();
const host = await startValidationHost(repoRoot);
const vars = varsFor(host);
const fixtures = await loadFixtures(flowId);
const flows = [];
let browser;

try {
  browser = await launchBrowser();
  for (const fixture of fixtures)
    flows.push(await runFlow(browser, host, fixture, outDir));
} finally {
  await browser?.close();
  await host.dispose();
}

const summary = summaryReport({
  startedAtUtc,
  finishedAtUtc: new Date().toISOString(),
  flows,
});
await writeJson(join(outDir, 'report.json'), summary);

const failed = flows.filter(flow => !flow.passed);
if (failed.length > 0) {
  console.error(`Mobile validation failed: ${failed.map(flow => flow.flowId).join(', ')}.`);
  process.exit(1);
}

console.log(`Mobile validation passed. report: ${join(outDir, 'report.json')}`);

async function loadFixtures(only) {
  const names = (await readdir(HERE))
    .filter(name => name.endsWith('.json'))
    .sort();
  const selected = only ? names.filter(name => name === `${only}.json`) : names;
  if (only && selected.length === 0)
    throw new Error(`Unknown Mobile flow '${only}'.`);
  return Promise.all(selected.map(async name => {
    const raw = JSON.parse(await readFile(join(HERE, name), 'utf8'));
    return interpolate(raw, vars);
  }));
}

async function launchBrowser() {
  try {
    return await chromium.launch({ channel: 'chromium', headless: true });
  } catch {
    return await chromium.launch({ headless: true });
  }
}

async function runFlow(browser, host, fixture, outDir) {
  const context = await browser.newContext({
    ...devices['Pixel 7'],
    viewport: { width: 390, height: 844 },
  });
  const page = await context.newPage();
  const report = emptyReport({ flowId: fixture.id });
  try {
    await page.goto(new URL(fixture.initialPath ?? '/connect', `${host.mobileUrl}/`).toString());
    await assertExpectations(page, fixture.initialExpectations ?? [], 'the initial place');
    let shot = 0;
    await capture(page, outDir, fixture.id, shot, 'initial', report, {
      id: 'initial',
      description: 'Arrive at the starting place',
      action: 'Open',
      target: null,
      expectations: fixture.initialExpectations ?? [],
    }, true);

    for (const step of fixture.steps ?? []) {
      const started = Date.now();
      shot += 1;
      try {
        await perform(page, host, step);
        await assertExpectations(page, step.expectations ?? [], `step ${step.id}`);
        await capture(page, outDir, fixture.id, shot, step.id, report, step, true, Date.now() - started);
      } catch (cause) {
        await capture(page, outDir, fixture.id, shot, step.id, report, step, false, Date.now() - started, cause);
        throw cause;
      }
    }

    const finished = finishReport(report, { passed: true });
    await writeJson(join(outDir, fixture.id, 'report.json'), finished);
    await writeServerLog(outDir, fixture.id, host);
    return finished;
  } catch (cause) {
    const finished = finishReport(report, {
      passed: false,
      error: cause instanceof Error ? cause.message : String(cause),
    });
    await writeJson(join(outDir, fixture.id, 'report.json'), finished);
    await writeServerLog(outDir, fixture.id, host);
    return finished;
  } finally {
    await context.close();
  }
}

async function perform(page, host, step) {
  const action = String(step.action ?? '').toLowerCase();
  if (action === 'fill') {
    await locatorFor(page, step.target).fill(String(step.value ?? ''));
    return;
  }
  if (action === 'click') {
    await locatorFor(page, step.target).click();
    return;
  }
  if (action === 'press') {
    await locatorFor(page, step.target).press(String(step.value ?? 'Enter'));
    return;
  }
  if (action === 'navigate') {
    await page.goto(new URL(step.value, `${host.mobileUrl}/`).toString());
    return;
  }
  throw new Error(`Unknown action '${step.action}' in step ${step.id}.`);
}

async function assertExpectations(page, expectations, description) {
  for (const expectation of expectations ?? []) {
    const kind = String(expectation.kind ?? '');
    if (kind === 'Url') {
      await waitFor(`${description} URL ${expectation.value}`, async () =>
        urlMatches(page.url(), expectation.value), { timeoutMs: 180_000 });
      continue;
    }
    if (kind === 'Title') {
      await waitFor(`${description} title ${expectation.value}`, async () =>
        (await page.title()).includes(expectation.value), { timeoutMs: 180_000 });
      continue;
    }
    if (kind === 'Text') {
      if (expectation.target) {
        const locator = locatorFor(page, expectation.target);
        await waitFor(`${description} text ${expectation.value}`, async () => {
          const value = await locator.inputValue().catch(() => null);
          const text = value ?? await locator.textContent();
          return typeof text === 'string' && text.includes(expectation.value);
        }, { timeoutMs: 180_000 });
        continue;
      }
      await page.getByText(expectation.value, { exact: false }).first().waitFor({ state: 'visible', timeout: 180_000 });
      continue;
    }
    if (kind === 'Visible') {
      const locator = expectation.target
        ? locatorFor(page, expectation.target)
        : page.getByText(expectation.value, { exact: false }).first();
      const shouldBeVisible = String(expectation.value ?? 'true').toLowerCase() !== 'false';
      await locator.waitFor({ state: shouldBeVisible ? 'visible' : 'hidden', timeout: 180_000 });
      continue;
    }
    if (kind === 'Running')
      throw new Error('Running expectations are for workspace applications, not the Mobile surface.');
    throw new Error(`Unknown expectation kind '${kind}'.`);
  }
}

function urlMatches(actual, expected) {
  const url = new URL(actual);
  const path = url.pathname.replace(/\/$/, '') || '/';
  const want = String(expected).startsWith('http')
    ? new URL(expected).pathname.replace(/\/$/, '') || '/'
    : String(expected).replace(/\/$/, '') || '/';
  return path === want || path.startsWith(`${want}/`) || path.endsWith(want);
}

async function capture(page, outDir, flowId, index, stepId, report, step, passed, durationMs = 0, cause = null) {
  const file = screenshotPath(outDir, flowId, index, stepId);
  await mkdir(dirname(file), { recursive: true });
  await page.screenshot({ path: file, fullPage: true });
  const resolved = await resolveTarget(page, step.target);
  report.steps.push({
    id: step.id,
    description: step.description,
    action: String(step.action ?? 'Open'),
    target: step.target ?? null,
    resolved,
    expectations: (step.expectations ?? []).map(expectation => ({
      kind: expectation.kind,
      value: expectation.value,
      passed,
      actual: expectation.kind === 'Url' ? new URL(page.url()).pathname : null,
      message: passed || !cause ? null : (cause instanceof Error ? cause.message : String(cause)),
    })),
    passed,
    durationMs,
    screenshot: relativeScreenshot(index, stepId),
  });
}

async function writeServerLog(outDir, flowId, host) {
  const text = typeof host.serverLog === 'function' ? host.serverLog() : '';
  await mkdir(join(outDir, flowId), { recursive: true });
  await writeFile(join(outDir, flowId, 'server.log'), `${text}\n`);
}

async function resolveTarget(page, target) {
  if (!target)
    return null;
  try {
    const locator = locatorFor(page, target).first();
    const box = await locator.boundingBox();
    const name = await locator.getAttribute('aria-label');
    const role = await locator.getAttribute('role');
    return {
      controlType: role,
      automationId: target.testId ?? null,
      name: name ?? target.name ?? target.label ?? null,
      x: box?.x ?? null,
      y: box?.y ?? null,
      width: box?.width ?? null,
      height: box?.height ?? null,
    };
  } catch {
    return null;
  }
}
