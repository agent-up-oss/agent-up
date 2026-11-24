// The semantics of AgentUp.FakeServer/parity.json: which operations the API reference documents,
// how a manifest entry becomes a concrete request against a backend's own fixture, and what an
// answer must look like. Pure functions, shared by the Server leg (.github/scripts/backend-parity.mjs)
// and Mobile's Demo test (FakeBackendParity.test.ts); Desktop's DemoParityTests.cs follows the same
// rules in C#. Definition: docs/developer-guide/repo/ci.md, Backend.

export const MANIFEST = 'AgentUp.FakeServer/parity.json';
export const API_REFERENCE = 'docs/api-reference/openapi.json';
const METHODS = ['get', 'post', 'put', 'delete', 'patch'];

/** Placeholder name to fixture token, unless an entry overrides it with `params`. */
export const DEFAULT_PARAMS = {
  workspaceId: 'workspace',
  id: 'workspace',
  application: 'application',
  name: 'application',
  sessionId: 'session',
  evidenceId: 'evidence',
  owner: 'owner',
  repository: 'repository',
};

/** Every operation the API reference documents, as "METHOD /template". */
export function operations(openapi) {
  const result = [];
  for (const [path, item] of Object.entries(openapi.paths ?? {}))
    for (const method of METHODS)
      if (item[method]) result.push(`${method.toUpperCase()} ${path}`);
  return result.sort();
}

/** Problems with the manifest against the API reference. Empty means complete and well formed. */
export function completeness(manifest, openapi) {
  const problems = [];
  const documented = new Set(operations(openapi));
  const seen = new Set();
  for (const entry of manifest.operations ?? []) {
    if (seen.has(entry.route)) problems.push(`${entry.route} is listed twice.`);
    seen.add(entry.route);
    if (!documented.has(entry.route)) problems.push(`${entry.route} is not in the API reference; remove it or regenerate the reference.`);
    const read = entry.route.startsWith('GET ');
    if (read && !entry.server) problems.push(`${entry.route} records no Server answer.`);
    if (!entry.demo) problems.push(`${entry.route} does not say whether the Demo answers it.`);
    else if (entry.demo !== true && typeof entry.demo === 'object' && !('status' in entry.demo) && !entry.demo.absent)
      problems.push(`${entry.route} gives the Demo neither an answer nor a reason it has none.`);
    if (!read && entry.demo !== true && !entry.demo?.absent)
      problems.push(`${entry.route} is a write: the Demo either answers it (true) or says why not (absent).`);
    for (const key of ['omits', 'extra'])
      if (entry.demo?.[key]?.length && !entry.demo.because)
        problems.push(`${entry.route} lets the Demo differ from the Server (${key}) without saying why.`);
  }
  for (const route of documented)
    if (!seen.has(route)) problems.push(`${route} is in the API reference but not in ${MANIFEST}: say whether the Demo answers it.`);
  return problems;
}

/** The concrete path and query for an entry, with fixture tokens resolved. */
export function concrete(entry, fixture) {
  const template = entry.route.slice(entry.route.indexOf(' ') + 1);
  const params = { ...DEFAULT_PARAMS, ...(entry.params ?? {}) };
  const path = template.replace(/\{([^}]+)\}/g, (_, name) => encodeURIComponent(fixture[params[name]] ?? 'absent'));
  const query = Object.entries(entry.query ?? {})
    .map(([name, token]) => `${name}=${encodeURIComponent(token ? (fixture[token] ?? '') : '')}`)
    .join('&');
  return { path, query: query ? `?${query}` : '' };
}

/** object, array, stream, text or empty, from a response's content type and text. */
export function bodyKind(contentType, text) {
  const type = String(contentType ?? '').toLowerCase();
  if (type.includes('text/event-stream')) return 'stream';
  if (type.includes('json') && !type.includes('ndjson') && !type.includes('jsonl')) {
    try {
      const value = JSON.parse(text);
      if (Array.isArray(value)) return 'array';
      if (value && typeof value === 'object') return 'object';
    } catch {
      return 'text';
    }
  }
  if (!text) return 'empty';
  return 'text';
}

export function keysOf(kind, text) {
  return kind === 'object' ? Object.keys(JSON.parse(text)).sort() : undefined;
}

/** What the Demo must answer a read with, derived from the Server's answer and the declared differences. */
export function expectedDemo(entry) {
  const demo = entry.demo;
  if (!demo || demo === true || demo.absent) return null;
  const keys = demo.keys
    ?? (entry.server?.keys
      ? [...new Set([...entry.server.keys.filter(key => !(demo.omits ?? []).includes(key)), ...(demo.extra ?? [])])].sort()
      : undefined);
  return { status: demo.status, body: demo.body ?? entry.server?.body, keys };
}

/** Problems with what one implementation answered, against what it must answer. */
export function compare(route, expected, observed) {
  const problems = [];
  if (expected.status !== observed.status)
    problems.push(`${route} answered ${observed.status}, expected ${expected.status}.`);
  if (expected.body && expected.body !== observed.body)
    problems.push(`${route} answered a${/^[aeiou]/.test(observed.body) ? 'n' : ''} ${observed.body} body, expected ${expected.body}.`);
  if (expected.keys && observed.keys) {
    const missing = expected.keys.filter(key => !observed.keys.includes(key));
    const extra = observed.keys.filter(key => !expected.keys.includes(key));
    if (missing.length) problems.push(`${route} is missing ${missing.join(', ')}.`);
    if (extra.length) problems.push(`${route} also returns ${extra.join(', ')}, which the manifest does not record.`);
  }
  return problems;
}
