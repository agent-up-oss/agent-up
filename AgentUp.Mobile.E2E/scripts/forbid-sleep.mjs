import { readdir, readFile } from 'node:fs/promises';
import { extname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Fails the build on a fixed sleep anywhere in this suite.
 *
 * A sleep is how an end-to-end suite becomes flaky: it passes on a fast runner, fails on a slow
 * one, and tells you nothing about what it was waiting for. Everything here waits on a condition
 * with a deadline instead, so this rule is enforced rather than merely asked for.
 */
const ROOT = fileURLToPath(new URL('..', import.meta.url));
const ALLOWED = new Set(['harness/wait.mjs', 'scripts/forbid-sleep.mjs']);
const SKIP_DIRECTORIES = new Set(['node_modules', '.git', 'artifacts']);
const PATTERNS = [
  { rule: /\bsetTimeout\s*\(/, why: 'setTimeout is a fixed delay; wait on a condition instead (harness/wait.mjs)' },
  { rule: /\bdevice\.sleep\s*\(/, why: "device.sleep is a fixed delay; use waitFor(...).withTimeout(...)" },
  { rule: /\bpage\.waitForTimeout\s*\(/, why: 'page.waitForTimeout is a fixed delay; wait on a locator or a response' },
];

const offences = [];

for await (const file of walk(ROOT)) {
  const path = relative(ROOT, file).split('\\').join('/');
  if (ALLOWED.has(path)) continue;
  if (!['.mjs', '.js', '.ts'].includes(extname(file))) continue;

  const lines = (await readFile(file, 'utf8')).split('\n');
  lines.forEach((line, index) => {
    for (const { rule, why } of PATTERNS) {
      if (rule.test(line)) offences.push(`${path}:${index + 1}  ${why}`);
    }
  });
}

if (offences.length > 0) {
  console.error('Fixed delays are not allowed in the end-to-end suite:\n');
  for (const offence of offences) console.error(`  ${offence}`);
  process.exit(1);
}

console.log('No fixed delays found.');

async function* walk(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (SKIP_DIRECTORIES.has(entry.name)) continue;
      yield* walk(join(directory, entry.name));
    } else {
      yield join(directory, entry.name);
    }
  }
}
