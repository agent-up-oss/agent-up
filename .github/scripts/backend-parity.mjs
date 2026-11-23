#!/usr/bin/env node
// Holds a real Server process to AgentUp.FakeServer/parity.json, the one client contract the Server
// and both Demo backends answer. The Demo legs hold Desktop's and Mobile's in-process Demo to the
// same file from their own test suites (DemoParityTests.cs, FakeBackendParity.test.ts).
//
// Every operation in the API reference appears in the manifest exactly once. A read records what
// the real Server answers it with - status, body kind and top-level keys - and what the Demo
// answers it with, or why the Demo does not answer it at all. A Server read that changes shape
// fails this leg until the manifest is re-recorded, and re-recording is what puts the new field in
// front of whoever decides whether the Demo shows it too.
//
//   node .github/scripts/backend-parity.mjs check
//   node .github/scripts/backend-parity.mjs server --url http://127.0.0.1:5199 --workspace <id> \
//     --application Web --changed-file probe.txt [--record]

import { readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { API_REFERENCE, MANIFEST, bodyKind, compare, completeness, concrete, keysOf } from '../../AgentUp.FakeServer/parity.mjs';

// The Server flushes an event stream's headers with its first event, so a stream with nothing to
// say yet answers nothing at all. A route that does not exist answers 404 at once; one still open
// after this long is a live stream.
const HELD_OPEN_MS = 5_000;

async function probe(base, entry, fixture) {
  const { path, query } = concrete(entry, fixture);
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), HELD_OPEN_MS);
  try {
    let response;
    try {
      response = await fetch(`${base}${path}${query}`, { signal: controller.signal });
    } catch (error) {
      if (error?.name === 'AbortError') return { status: 200, body: 'stream' };
      throw error;
    }
    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('text/event-stream')) {
      controller.abort();
      return { status: response.status, body: 'stream' };
    }
    const text = await response.text();
    const body = bodyKind(contentType, text);
    return { status: response.status, body, keys: keysOf(body, text) };
  } finally {
    clearTimeout(timer);
  }
}

function argument(args, name, fallback) {
  const index = args.indexOf(`--${name}`);
  return index >= 0 ? args[index + 1] : fallback;
}

async function main(args) {
  const [command] = args;
  const manifest = JSON.parse(readFileSync(MANIFEST, 'utf8'));
  const openapi = JSON.parse(readFileSync(API_REFERENCE, 'utf8'));
  const record = args.includes('--record');
  // Recording is how a read gets its Server answer, so a missing one is not a problem then.
  const problems = completeness(manifest, openapi)
    .filter(problem => !(record && problem.endsWith('records no Server answer.')));
  if (command === 'check' || problems.length > 0) {
    for (const problem of problems) console.error(problem);
    if (problems.length === 0) console.log(`${manifest.operations.length} operations classified against the API reference.`);
    return problems.length === 0 ? 0 : 1;
  }
  if (command !== 'server') {
    console.error('Usage: backend-parity.mjs check | server --url <base> --workspace <id> [--application <name>] [--changed-file <path>] [--record]');
    return 2;
  }

  const base = argument(args, 'url');
  const fixture = {
    ...manifest.fixtures.server,
    workspace: argument(args, 'workspace'),
    application: argument(args, 'application', manifest.fixtures.server.application),
    changedFile: argument(args, 'changed-file', manifest.fixtures.server.changedFile),
  };
  const failures = [];
  const rows = [];
  for (const entry of manifest.operations.filter(item => item.route.startsWith('GET '))) {
    const observed = await probe(base, entry, fixture);
    if (record) {
      entry.server = { status: observed.status, body: observed.body, ...(observed.keys ? { keys: observed.keys } : {}) };
      continue;
    }
    const found = compare(entry.route, entry.server, observed);
    failures.push(...found);
    rows.push(`| ${found.length ? '❌' : '✅'} | \`${entry.route}\` | ${observed.status} ${observed.body} | ${entry.demo?.absent ? 'server only' : 'also Demo'} |`);
  }
  if (record) {
    writeFileSync(MANIFEST, `${JSON.stringify(manifest, null, 2)}\n`);
    console.log(`Recorded the Server's answers into ${MANIFEST}.`);
    return 0;
  }

  const reads = rows.length;
  const summary = [
    `### Server against ${MANIFEST}`, '',
    `${reads - new Set(failures.map(line => line.split(' ')[1])).size} of ${reads} reads answered as recorded.`, '',
    '| | Read | Answer | |', '|---|---|---|---|', ...rows, '',
  ].join('\n');
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);
  for (const failure of failures) console.error(failure);
  if (failures.length) {
    console.error(`\nIf the Server changed on purpose, re-record with --record and decide, per changed read, whether the Demo answers it too.`);
    return 1;
  }
  console.log(`All ${reads} reads answered as recorded.`);
  return 0;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1])
  main(process.argv.slice(2)).then(code => { process.exitCode = code; });
