import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';

export function emptyReport({ flowId, surface = 'Mobile', startedAtUtc = new Date().toISOString() }) {
  return {
    flowId,
    surface,
    passed: true,
    startedAtUtc,
    finishedAtUtc: startedAtUtc,
    steps: [],
    error: null,
  };
}

export function finishReport(report, { passed, error = null, finishedAtUtc = new Date().toISOString() }) {
  return {
    ...report,
    passed,
    finishedAtUtc,
    error,
  };
}

export function summaryReport({ startedAtUtc, finishedAtUtc, flows }) {
  return {
    surface: 'Mobile',
    passed: flows.every(flow => flow.passed),
    startedAtUtc,
    finishedAtUtc,
    flows,
  };
}

export async function writeJson(path, value) {
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, `${JSON.stringify(value, null, 2)}\n`);
}

export function screenshotName(index, stepId) {
  const safe = String(stepId ?? 'step').replace(/[^A-Za-z0-9._-]+/g, '_');
  if (index === 0)
    return '00-initial.png';
  return `${String(index).padStart(2, '0')}-${safe}.png`;
}

export function screenshotPath(outDir, flowId, index, stepId) {
  return join(outDir, flowId, screenshotName(index, stepId));
}

export function relativeScreenshot(index, stepId) {
  return screenshotName(index, stepId);
}
