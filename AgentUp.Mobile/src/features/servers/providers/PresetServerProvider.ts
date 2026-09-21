import { normalizeServerUrl } from './ServerUrlProvider';

export type PresetServerTarget =
  | { kind: 'cloud'; url: string }
  | { kind: 'selfHosted'; url: string };

const pendingWorkspaceKey = 'agent-up.preset.workspace';

export function resolvePresetServerUrl(value: string, cloudUrl?: string): PresetServerTarget {
  const url = normalizeServerUrl(value);
  if (cloudUrl && url === normalizeServerUrl(cloudUrl))
    return { kind: 'cloud', url };
  return { kind: 'selfHosted', url };
}

export function rememberPresetWorkspace(id?: string): void {
  if (typeof sessionStorage === 'undefined') return;
  const trimmed = id?.trim();
  if (!trimmed) {
    sessionStorage.removeItem(pendingWorkspaceKey);
    return;
  }
  sessionStorage.setItem(pendingWorkspaceKey, trimmed);
}

export function takePendingWorkspace(): string | null {
  if (typeof sessionStorage === 'undefined') return null;
  const value = sessionStorage.getItem(pendingWorkspaceKey);
  sessionStorage.removeItem(pendingWorkspaceKey);
  return value;
}

export function workspaceHref(workspaceId?: string | null): string {
  const id = workspaceId?.trim();
  return id ? `/(main)/workspace/${encodeURIComponent(id)}` : '/(main)/workspace';
}
