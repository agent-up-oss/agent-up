import type { WorkspaceApplication } from '@/features/workspaces/models/Workspace';
import type { ValidationAssertion, ValidationFlow, ValidationStage } from '../models/ValidationFlow';

const InitialStageId = 'initial';

/** HTTP Process webviews host Validation. Desktop streams and database apps do not. */
export function showsValidationChrome(application: WorkspaceApplication | undefined): boolean {
  return application != null
    && application.kind !== 'Desktop'
    && application.database !== true;
}

export function validationToggleLabel(open: boolean): string {
  return open ? 'Close validation' : 'Open validation';
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
