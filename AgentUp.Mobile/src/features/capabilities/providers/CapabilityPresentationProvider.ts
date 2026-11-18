import type { CapabilityModule, CapabilitySetting } from '../models/CapabilityModule';

/** A Server that predates built-in capabilities sends no `available`; its modules are treated as found. */
export function isAvailable(module: CapabilityModule): boolean {
  return module.available !== false;
}

/**
 * The capability's one button. Software that was not found is said in the button itself, short,
 * instead of offering Enable for the Server to refuse.
 */
export function capabilityButton(module: CapabilityModule): { label: string; disabled: boolean } {
  if (module.enabled) return { label: 'Disable', disabled: false };
  if (isAvailable(module)) return { label: 'Enable', disabled: false };
  return { label: (module.missing ?? []).includes('nix') ? 'Needs Nix' : 'Not found', disabled: true };
}

export function capabilityStatus(module: CapabilityModule): string {
  switch (module.state) {
    case 'ready': return 'Ready';
    case 'needsSetup': return 'Needs setup';
    case 'unavailable': return (module.missing ?? []).length > 0 ? `Missing ${(module.missing ?? []).join(', ')}` : 'Not found';
    default: return module.canRun ? 'Ready' : 'Off';
  }
}

/** Settings appear once a capability is enabled, because they only matter then. */
export function showsSettings(module: CapabilityModule): boolean {
  return module.enabled && (module.settings ?? []).length > 0;
}

/** What a field starts with: the stored value, or nothing for a secret the Server never returns. */
export function initialValue(setting: CapabilitySetting): string {
  return setting.kind === 'secret' ? '' : setting.value ?? '';
}

export function settingPlaceholder(setting: CapabilitySetting): string {
  if (setting.kind === 'secret' && setting.hasValue) return 'Stored';
  return setting.placeholder ?? '';
}

export function settingLabel(setting: CapabilitySetting): string {
  return setting.required ? setting.label : `${setting.label} (optional)`;
}

/** Only edited fields are sent; an untouched secret is left out so the stored one is kept. */
export function changedSettings(settings: CapabilitySetting[], draft: Record<string, string>): Record<string, string> {
  const changed: Record<string, string> = {};
  for (const setting of settings) {
    const value = draft[setting.key];
    if (value === undefined) continue;
    const edited = setting.kind === 'secret' ? value.length > 0 : value !== initialValue(setting);
    if (edited) changed[setting.key] = value.trim();
  }
  return changed;
}
