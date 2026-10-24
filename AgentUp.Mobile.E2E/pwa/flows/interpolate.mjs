/**
 * Replaces `{serverUrl}`, `{workspaceId}`, and `{workspaceName}` in a flow so the same JSON
 * can run against an ephemeral Server without baking ports or ids into the fixture.
 */
export function interpolate(value, vars) {
  if (value == null || typeof value === 'number' || typeof value === 'boolean')
    return value;
  if (typeof value === 'string')
    return value.replaceAll(/\{([A-Za-z]+)\}/g, (match, key) => {
      if (!Object.hasOwn(vars, key))
        throw new Error(`Unknown placeholder ${match}.`);
      return String(vars[key]);
    });
  if (Array.isArray(value))
    return value.map(item => interpolate(item, vars));
  if (typeof value === 'object') {
    const next = {};
    for (const [key, item] of Object.entries(value))
      next[key] = interpolate(item, vars);
    return next;
  }
  return value;
}
