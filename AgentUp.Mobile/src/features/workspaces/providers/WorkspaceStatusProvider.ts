import { auBox } from '@agent-up/design-system/native';

// Desktop's AppHealthLedRules maps Healthy/Running to green, Checking to yellow, and
// Unhealthy/Failed to red. Starting and Stopping are muted there until port probes publish
// Checking. Mobile paints those lifecycle states as warning too so the LED moves as soon as
// the user starts or stops, then follows healthState whenever the Server stream supplies it.
export type StatusLedRole = 'healthy' | 'warning' | 'danger' | 'idle';

export type WorkspaceLifecycleControls = {
  action: 'start' | 'stop';
  glyph: '▶' | '■';
  accessibilityLabel: string;
  busy: boolean;
};

export function statusLedRole(state: string | null | undefined): StatusLedRole {
  switch (state) {
    case 'Healthy':
    case 'Running':
      return 'healthy';
    case 'Checking':
    case 'Starting':
    case 'Stopping':
      return 'warning';
    case 'Unhealthy':
    case 'Failed':
      return 'danger';
    default:
      return 'idle';
  }
}

export function workspaceLedState(state: string, healthState?: string | null): string {
  // Leftover Healthy from a previous run must not paint Stopped or Failed green.
  if (state === 'Running') return healthState || state;
  return state;
}

export function workspaceStatusLabel(state: string, healthState?: string | null): string {
  if (state === 'Running' && healthState) return healthState;
  return state;
}

export function workspaceShowsApplications(state: string): boolean {
  return state === 'Running' || state === 'Starting';
}

export function workspaceLifecycleControls(state: string): WorkspaceLifecycleControls {
  const busy = state === 'Starting' || state === 'Stopping';
  if (state === 'Running' || state === 'Starting') {
    return { action: 'stop', glyph: '■', accessibilityLabel: 'Stop workspace', busy };
  }

  return { action: 'start', glyph: '▶', accessibilityLabel: 'Start workspace', busy };
}

export function statusDotStyle(state: string | null | undefined) {
  const role = statusLedRole(state);
  if (role === 'healthy') return auBox('statusDot', 'statusDotHealthy');
  if (role === 'warning') return auBox('statusDot', 'statusDotWarning');
  if (role === 'danger') return auBox('statusDot', 'statusDotDanger');
  return auBox('statusDot');
}
