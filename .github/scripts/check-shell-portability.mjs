#!/usr/bin/env node
/**
 * Keeps the CI shell scripts inside what the macOS runner can run.
 *
 * `shell: bash` on a macOS runner is /bin/bash, and Apple still ships 3.2 - the last release under
 * GPLv2, from 2006. Everything bash gained afterwards is simply absent there, and absent in the
 * least helpful way: `mapfile: command not found`, at the first line that uses it, in a job that
 * had not started doing anything yet. That is how the iOS job died once already.
 *
 * Linux runners have bash 5, so a script tested only there proves nothing about the runner that
 * matters. This checks mechanically instead of by memory.
 */
import { readFileSync } from 'node:fs';
import { globSync } from 'node:fs';

// Each is unavailable in 3.2 and named by the version that introduced it, so a failure says what
// to reach for instead rather than only what not to use.
const NEWER_THAN_32 = [
  { since: '4.0', pattern: /\b(mapfile|readarray)\b/, instead: 'while IFS= read -r line; do ... done <<< "$text"' },
  { since: '4.0', pattern: /\b(declare|typeset|local)\s+-[A-Za-z]*A/, instead: 'parallel plain variables, or a case statement' },
  { since: '4.0', pattern: /\$\{[A-Za-z_][A-Za-z0-9_]*(\[[^\]]*\])?(\^\^?|,,?)\}/, instead: "tr '[:upper:]' '[:lower:]'" },
  { since: '4.0', pattern: /&>>/, instead: '>>file 2>&1' },
  { since: '4.0', pattern: /\|&/, instead: '2>&1 |' },
  { since: '4.0', pattern: /\bcoproc\b/, instead: 'a background job and a named pipe' },
  { since: '4.1', pattern: /\bexec\s+\{[A-Za-z_][A-Za-z0-9_]*\}[<>]/, instead: 'a fixed file descriptor number' },
  { since: '4.1', pattern: /\bread\b[^\n#]*\s-[A-Za-z]*N/, instead: 'read -n' },
  { since: '4.2', pattern: /\[\[?\s+-v\s/, instead: '[ -n "${name+set}" ]' },
  { since: '4.2', pattern: /\$\{[A-Za-z_][A-Za-z0-9_]*\[-[0-9]+\]\}/, instead: 'a positive index' },
  { since: '4.3', pattern: /\bwait\s+-n\b/, instead: 'wait for each pid' },
  { since: '4.4', pattern: /\$\{[A-Za-z_][A-Za-z0-9_]*(\[[^\]]*\])?@[QEPAKa]\}/, instead: 'printf %q' },
];

// Whole-line comments are not code. This is deliberately narrow: a `#` inside a string is not a
// comment, and guessing which is which would be a worse check than no check.
const isComment = line => /^\s*#/.test(line);

const scripts = globSync('.github/scripts/*.sh').sort();
if (scripts.length === 0) {
  console.error('No CI shell scripts were found to check, which is not something to pass quietly.');
  process.exit(2);
}

let found = 0;
for (const script of scripts) {
  readFileSync(script, 'utf8').split('\n').forEach((line, index) => {
    if (isComment(line)) return;
    for (const { since, pattern, instead } of NEWER_THAN_32) {
      const hit = line.match(pattern);
      if (!hit) continue;
      found += 1;
      console.error(`${script}:${index + 1}: '${hit[0]}' is bash ${since}; the macOS runner has 3.2.`);
      console.error(`  Use instead: ${instead}`);
      console.error(`  ${line.trim()}`);
    }
  });
}

if (found > 0) {
  console.error(`\n${found} construct(s) the macOS runner cannot run.`);
  process.exit(1);
}

console.log(`${scripts.length} CI shell scripts stay within bash 3.2.`);
