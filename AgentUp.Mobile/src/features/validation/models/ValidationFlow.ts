export type ValidationAction = 'Navigate' | 'Click' | 'Fill' | 'Press';

export type ValidationExpectation =
  | 'Url'
  | 'Title'
  | 'Text'
  | 'Visible'
  | 'Running'
  | 'Enabled'
  | 'State'
  | 'Absent';

export type ValidationSurface = 'Application' | 'Desktop' | 'Mobile';

export type ValidationTarget = {
  role?: string | null;
  name?: string | null;
  label?: string | null;
  testId?: string | null;
  text?: string | null;
  selector?: string | null;
};

export type ValidationAssertion = {
  kind: ValidationExpectation;
  value: string;
  target?: ValidationTarget | null;
};

export type ValidationStep = {
  id: string;
  description: string;
  action: ValidationAction;
  target?: ValidationTarget | null;
  value?: string | null;
  expectations?: ValidationAssertion[] | null;
};

export type ValidationFlow = {
  id: string;
  workspaceId: string;
  application: string;
  name: string;
  description: string;
  initialPath: string;
  initialExpectations: ValidationAssertion[];
  steps: ValidationStep[];
  version: number;
  surface?: ValidationSurface;
};

export type ValidationStage = {
  id: string;
  title: string;
  checks: string[];
};
