#!/usr/bin/env node
// Names the tests that failed, in the job summary rather than only in the log.
//
// A flaky suite is only fixable if the next occurrence says which test it was. The log cannot be
// relied on for that: the coverage job uploads eighteen reports after the tests run, and the
// retrievable tail of the log covered forty-four seconds of uploading the last time a test failed
// there - the failure itself had already scrolled out of reach. The job summary is written once and
// stays whole.

import { readdir, readFile, appendFile } from "node:fs/promises";
import { join } from "node:path";

const root = process.argv[2] ?? "artifacts/test-results";

async function trxFiles(directory) {
  let entries;
  try {
    entries = await readdir(directory, { withFileTypes: true });
  } catch {
    return [];
  }

  const files = [];
  for (const entry of entries) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) files.push(...(await trxFiles(path)));
    else if (entry.name.endsWith(".trx")) files.push(path);
  }
  return files;
}

const escapeHtml = (text) => text.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");

const unescape = (text) =>
  text
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&quot;", '"')
    .replaceAll("&apos;", "'")
    .replaceAll("&#xD;", "")
    .replaceAll("&amp;", "&");

function element(source, name) {
  const match = source.match(new RegExp(`<${name}>([\\s\\S]*?)</${name}>`));
  return match ? unescape(match[1]).trim() : "";
}

function failures(trx) {
  const found = [];
  // Attributes are walked quote-aware: a test name can carry a '>' (a generic argument, an
  // assertion spelled into the name) and XML does not require it escaped inside an attribute.
  const open = /<UnitTestResult\b((?:"[^"]*"|[^>"])*?)(\/?)>/g;
  for (let tag = open.exec(trx); tag !== null; tag = open.exec(trx)) {
    const [, attributes, selfClosing] = tag;
    // A passing test is written self-closing, so it carries no ErrorInfo to report.
    if (selfClosing === "/" || !/\boutcome="Failed"/.test(attributes)) continue;

    const close = trx.indexOf("</UnitTestResult>", open.lastIndex);
    const body = trx.slice(tag.index + tag[0].length, close === -1 ? undefined : close);
    const name = attributes.match(/\btestName="([^"]*)"/);
    found.push({
      name: name ? unescape(name[1]) : "(unnamed test)",
      message: element(body, "Message"),
      stackTrace: element(body, "StackTrace"),
    });
  }
  return found;
}

const files = await trxFiles(root);
const reported = [];
// A data-driven test is recorded twice - once on the aggregate result, once on the inner case that
// actually failed - and the two carry the same error. Report each distinct failure once.
const seen = new Set();
for (const file of files) {
  for (const failure of failures(await readFile(file, "utf8"))) {
    const key = `${file}\u0000${failure.name}\u0000${failure.message}`;
    if (seen.has(key)) continue;
    seen.add(key);
    reported.push({ ...failure, suite: file });
  }
}

if (reported.length === 0) {
  // A suite that dies before it writes its TRX - a build error, a crashed host - leaves nothing to
  // read. Say so, rather than implying the run was clean.
  console.log(`No failed tests recorded in ${files.length} result file(s) under ${root}.`);
  process.exit(0);
}

const plain = reported
  .map((f) => `${f.name}\n  in ${f.suite}\n  ${f.message.split("\n").join("\n  ")}`)
  .join("\n\n");
console.log(`\n${reported.length} failed test(s):\n\n${plain}\n`);

const summary = process.env.GITHUB_STEP_SUMMARY;
if (!summary) process.exit(0);

const markdown = [
  `### ${reported.length} failed test${reported.length === 1 ? "" : "s"}`,
  "",
  ...reported.flatMap((f) => [
    `<details><summary><code>${escapeHtml(f.name)}</code></summary>`,
    "",
    `Recorded in \`${f.suite}\`.`,
    "",
    "```",
    [f.message, f.stackTrace].filter(Boolean).join("\n\n") || "(no error detail recorded)",
    "```",
    "",
    "</details>",
    "",
  ]),
].join("\n");

await appendFile(summary, `${markdown}\n`);
