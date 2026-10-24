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

export function screenshotPath(outDir, flowId, stepId) {
  return join(outDir, flowId, `${stepId}.png`);
}

export function relativeScreenshot(flowId, stepId) {
  return `${flowId}/${stepId}.png`;
}
