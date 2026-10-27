import type { WorkspaceApplication } from '@/features/workspaces/models/Workspace';
import type {
  ValidationAssertion,
  ValidationFlow,
  ValidationRunState,
  ValidationStage,
  ValidationStageView,
} from '../models/ValidationFlow';

export const InitialStageId = 'initial';

/** HTTP Process webviews host Validation. Desktop streams and database apps do not. */
export function showsValidationChrome(application: WorkspaceApplication | undefined): boolean {
  return application != null
    && application.kind !== 'Desktop'
    && application.database !== true;
}

export function validationMenuLabel(open: boolean): string {
  return open ? 'Hide validation scenarios' : 'Show validation scenarios';
}

/** Turns a flow into the starting-page stage plus one stage per recorded step. */
export function validationStages(flow: ValidationFlow): ValidationStage[] {
  return [
    {
      id: InitialStageId,
      title: 'Starting page',
      checks: (flow.initialExpectations ?? []).map(describeAssertion),
    },
    ...(flow.steps ?? []).map(step => ({
      id: step.id,
      title: step.description,
      checks: (step.expectations ?? []).map(describeAssertion),
    })),
  ];
}

export function idleStageViews(flow: ValidationFlow): ValidationStageView[] {
  return validationStages(flow).map(stage => ({
    id: stage.id,
    title: stage.title,
    state: 'pending',
    checks: stage.checks.map(label => ({ label, state: 'pending' as const })),
  }));
}

export function adjacentFlowIndex(index: number, count: number, delta: number): number {
  if (count <= 0) return 0;
  return ((index + delta) % count + count) % count;
}

/** Maps a vertical drag to next (+1) or previous (-1). Zero when the gesture is too small. */
export function swipeFlowDelta(displacement: number, threshold = 24): number {
  if (Math.abs(displacement) < threshold) return 0;
  return displacement < 0 ? 1 : -1;
}

export function selectedFlowSubstep(
  stages: ValidationStageView[],
  resultMessage: string | null,
): string {
  for (const stage of stages) {
    const running = stage.checks.find(check => check.state === 'running');
    if (running) return running.label;
  }
  const runningStage = stages.find(stage => stage.state === 'running');
  if (runningStage) return runningStage.title;
  if (resultMessage) return resultMessage;
  return stages[0]?.title ?? '';
}

export function compactValidationCopy(input: {
  name: string;
  stages: ValidationStageView[];
  resultMessage: string | null;
}): { title: string; substep: string } {
  return {
    title: input.name,
    substep: selectedFlowSubstep(input.stages, input.resultMessage),
  };
}

export function validationRunGlyph(state: ValidationRunState): string {
  if (state === 'passed') return '✓';
  if (state === 'failed') return '✗';
  if (state === 'running') return '●';
  return '○';
}

export function describeAssertion(assertion: ValidationAssertion): string {
  const kind = assertion.kind;
  const value = assertion.value;
  if (kind === 'Title') return `Title is "${value}"`;
  if (kind === 'Text') return `Text contains "${value}"`;
  if (kind === 'Url') return `URL is "${value}"`;
  if (kind === 'Visible') return `${targetLabel(assertion)} is ${value === 'true' ? 'visible' : 'hidden'}`;
  if (kind === 'Enabled') return `${targetLabel(assertion)} is ${value === 'true' ? 'enabled' : 'disabled'}`;
  if (kind === 'Running') return `${targetLabel(assertion)} is ${value === 'true' ? 'running' : 'stopped'}`;
  if (kind === 'Absent') return `${targetLabel(assertion)} is absent`;
  if (kind === 'State') return `${targetLabel(assertion)} state is "${value}"`;
  return `${kind}: ${value}`;
}

function targetLabel(assertion: ValidationAssertion): string {
  const target = assertion.target;
  return target?.name || target?.label || target?.text || target?.testId || target?.role || 'Control';
}
