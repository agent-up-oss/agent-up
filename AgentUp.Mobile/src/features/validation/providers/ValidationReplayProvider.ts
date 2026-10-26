import type { ValidationAssertion, ValidationFlow, ValidationStep, ValidationTarget } from '../models/ValidationFlow';
import { InitialStageId } from './ValidationPanelProvider';
import {
  ReplayAnimationMs,
  beginAttentionPingScript,
  beginMouseMoveScript,
  checkNavigationScript,
  checkTextScript,
  checkVisibleScript,
  completeClickScript,
  fillScript,
  getTitleScript,
  getUrlScript,
  pressScript,
  removeAttentionPingScript,
  replayTarget,
} from './ValidationReplayScripts';

export type ValidationReplayHost = {
  eval(script: string): Promise<string | null>;
  delay(ms: number): Promise<void>;
};

export type ValidationReplayReporter = {
  beginFlow(): void;
  beginStage(stageId: string): void;
  beginCheck(stageId: string, checkIndex: number): void;
  completeCheck(stageId: string, checkIndex: number, passed: boolean, error?: string | null): void;
  completeStage(stageId: string, passed: boolean, error?: string | null): void;
  completeFlow(passed: boolean, message?: string | null): void;
};

export type ValidationReplayResult = {
  succeeded: boolean;
  message: string;
  stepId?: string;
};

export type RunValidationReplayOptions = {
  flow: ValidationFlow;
  host: ValidationReplayHost;
  reporter: ValidationReplayReporter;
  signal?: AbortSignal;
  skipCosmeticStaging?: boolean;
};

/** Replays an Application-surface flow in the visible webview, matching Desktop cursor staging. */
export async function runValidationReplay(options: RunValidationReplayOptions): Promise<ValidationReplayResult> {
  const { flow, host, reporter, signal, skipCosmeticStaging } = options;
  reporter.beginFlow();
  try {
    await waitForPageReady(host, signal);
    reporter.beginStage(InitialStageId);
    const initialError = await assertAll(host, InitialStageId, flow.initialExpectations ?? [], reporter, signal);
    if (initialError) {
      reporter.completeStage(InitialStageId, false, initialError);
      reporter.completeFlow(false, initialError);
      return { succeeded: false, message: initialError };
    }
    reporter.completeStage(InitialStageId, true);

    for (const step of flow.steps ?? []) {
      reporter.beginStage(step.id);
      const stepError = await executeStep(host, step, skipCosmeticStaging === true, signal);
      if (stepError) {
        reporter.completeStage(step.id, false, stepError);
        reporter.completeFlow(false, stepError);
        return { succeeded: false, message: stepError, stepId: step.id };
      }
      const assertionError = await assertAll(host, step.id, step.expectations ?? [], reporter, signal);
      if (assertionError) {
        reporter.completeStage(step.id, false, assertionError);
        reporter.completeFlow(false, assertionError);
        return { succeeded: false, message: assertionError, stepId: step.id };
      }
      reporter.completeStage(step.id, true);
    }

    const message = `Validation '${flow.name}' passed.`;
    reporter.completeFlow(true, message);
    return { succeeded: true, message };
  } catch (reason) {
    if (signal?.aborted || (reason instanceof Error && reason.name === 'AbortError')) {
      const message = 'Validation replay was cancelled.';
      reporter.completeFlow(false, message);
      return { succeeded: false, message };
    }
    throw reason;
  }
}

async function executeStep(
  host: ValidationReplayHost,
  step: ValidationStep,
  skipCosmeticStaging: boolean,
  signal?: AbortSignal,
): Promise<string | null> {
  throwIfAborted(signal);
  if (step.action === 'Navigate') {
    await waitForPageReady(host, signal);
    return null;
  }
  if (step.action === 'Press') {
    return executeScript(host, pressScript(step.value ?? 'Enter'), signal);
  }
  const target = clickTarget(step.target);
  if (target == null) return 'Watched replay requires a selector fallback for click and fill steps.';
  if (step.action === 'Click') return stagedClick(host, target, skipCosmeticStaging, signal);
  if (step.action === 'Fill') return stagedFill(host, target, step.value ?? '', skipCosmeticStaging, signal);
  return 'Watched replay requires a selector fallback for click and fill steps.';
}

function clickTarget(target: ValidationTarget | null | undefined): ValidationTarget | null {
  const next = replayTarget(target);
  if (next.selector || next.testId || next.name || next.label || next.text || next.role) return next;
  return null;
}

async function stagedClick(
  host: ValidationReplayHost,
  target: ValidationTarget,
  skipCosmeticStaging: boolean,
  signal?: AbortSignal,
): Promise<string | null> {
  if (!skipCosmeticStaging) {
    const moveError = await executeScript(host, beginMouseMoveScript(target), signal);
    if (moveError) return moveError;
    await host.delay(ReplayAnimationMs);
    const pingError = await executeScript(host, beginAttentionPingScript(target), signal);
    if (pingError) return pingError;
    await host.delay(ReplayAnimationMs);
  }
  const clickError = await executeScript(host, completeClickScript(target), signal);
  if (clickError) return clickError;
  if (!skipCosmeticStaging) await host.delay(ReplayAnimationMs);
  await waitForPageReady(host, signal);
  return null;
}

async function stagedFill(
  host: ValidationReplayHost,
  target: ValidationTarget,
  text: string,
  skipCosmeticStaging: boolean,
  signal?: AbortSignal,
): Promise<string | null> {
  if (!skipCosmeticStaging) {
    const moveError = await executeScript(host, beginMouseMoveScript(target), signal);
    if (moveError) return moveError;
    await host.delay(ReplayAnimationMs);
    const pingError = await executeScript(host, beginAttentionPingScript(target), signal);
    if (pingError) return pingError;
    await host.delay(ReplayAnimationMs);
  }
  const fillError = await executeScript(host, fillScript(target, text), signal);
  if (!skipCosmeticStaging) await executeScript(host, removeAttentionPingScript(), signal);
  return fillError;
}

async function assertAll(
  host: ValidationReplayHost,
  stageId: string,
  assertions: ValidationAssertion[],
  reporter: ValidationReplayReporter,
  signal?: AbortSignal,
): Promise<string | null> {
  for (let index = 0; index < assertions.length; index++) {
    reporter.beginCheck(stageId, index);
    const assertion = assertions[index];
    const error = await assertOne(host, assertion, signal);
    reporter.completeCheck(stageId, index, error == null, error);
    if (error) return error;
  }
  return null;
}

async function assertOne(
  host: ValidationReplayHost,
  assertion: ValidationAssertion,
  signal?: AbortSignal,
): Promise<string | null> {
  if (assertion.kind === 'Text') {
    return waitForCondition(host, checkTextScript(assertion.value), signal, 'Expected content did not appear in time.');
  }
  if (assertion.kind === 'Title') {
    return waitForTitle(host, assertion.value, signal);
  }
  if (assertion.kind === 'Url') {
    return waitForUrl(host, assertion.value, signal);
  }
  if (assertion.kind === 'Visible' && assertion.target) {
    const wanted = assertion.value !== 'false';
    return waitForCondition(
      host,
      checkVisibleScript(assertion.target),
      signal,
      wanted ? 'Expected the control to become visible.' : 'Expected the control to hide.',
      wanted,
    );
  }
  return 'Watched replay requires a selector fallback for visibility checks.';
}

async function waitForTitle(host: ValidationReplayHost, expected: string, signal?: AbortSignal): Promise<string | null> {
  for (let attempt = 0; attempt < 40; attempt++) {
    throwIfAborted(signal);
    const actual = trimQuotes(await host.eval(getTitleScript()));
    if (actual === expected) return null;
    await host.delay(250);
  }
  return `Expected title '${expected}', but the page did not reach it in time.`;
}

async function waitForUrl(host: ValidationReplayHost, expected: string, signal?: AbortSignal): Promise<string | null> {
  for (let attempt = 0; attempt < 40; attempt++) {
    throwIfAborted(signal);
    const actual = trimQuotes(await host.eval(getUrlScript()));
    if (actual === expected) return null;
    if (expected.startsWith('/') && urlPath(actual) === expected) return null;
    await host.delay(250);
  }
  return `Expected url '${expected}', but the page did not reach it in time.`;
}

async function waitForCondition(
  host: ValidationReplayHost,
  predicateScript: string,
  signal: AbortSignal | undefined,
  timeoutMessage: string,
  expected = true,
): Promise<string | null> {
  const wanted = expected ? 'true' : 'false';
  for (let attempt = 0; attempt < 40; attempt++) {
    throwIfAborted(signal);
    const result = (await host.eval(predicateScript))?.trim();
    if (result?.toLowerCase() === wanted) return null;
    await host.delay(250);
  }
  return timeoutMessage;
}

async function waitForPageReady(host: ValidationReplayHost, signal?: AbortSignal): Promise<void> {
  for (let attempt = 0; attempt < 40; attempt++) {
    throwIfAborted(signal);
    const state = trimQuotes(await host.eval(checkNavigationScript()));
    if (state.toLowerCase() === 'complete' || state.toLowerCase() === 'interactive') return;
    await host.delay(125);
  }
}

async function executeScript(host: ValidationReplayHost, script: string, signal?: AbortSignal): Promise<string | null> {
  throwIfAborted(signal);
  return parseScriptError(await host.eval(script));
}

export function parseScriptError(json: string | null): string | null {
  if (json == null || json.trim() === '') return 'Browser script returned no result.';
  if (!json.trimStart().startsWith('{')) return null;
  try {
    const parsed = JSON.parse(json) as { error?: string };
    return parsed.error ?? null;
  } catch {
    return null;
  }
}

function trimQuotes(value: string | null | undefined): string {
  return (value ?? '').trim().replace(/^"|"$/g, '');
}

function urlPath(value: string): string {
  try {
    return new URL(value).pathname + new URL(value).search;
  } catch {
    return value;
  }
}

function throwIfAborted(signal?: AbortSignal): void {
  if (!signal?.aborted) return;
  const error = new Error('Validation replay was cancelled.');
  error.name = 'AbortError';
  throw error;
}
