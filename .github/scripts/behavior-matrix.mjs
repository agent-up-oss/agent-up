#!/usr/bin/env node
// Renders .github/behavior-matrix.json with the verdicts this run's legs recorded, and decides
// whether the run proved what the matrix says it proves.
//
// A leg is proven only by its own verdict. A leg with no verdict did not run - it was skipped,
// cancelled, or failed before it could record one - and that fails the gate exactly like a leg
// that ran and failed. A green run therefore means every declared leg ran and passed, never that
// a leg quietly decided it had nothing to do.
//
//   node .github/scripts/behavior-matrix.mjs render \
//     --matrix .github/behavior-matrix.json --verdicts verdicts [--needs needs.json] [--summary out.md]
//   node .github/scripts/behavior-matrix.mjs check --matrix .github/behavior-matrix.json

import { readFileSync, readdirSync, appendFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ICON = {
  success: '✅',
  failure: '❌',
  cancelled: '⏹️',
  skipped: '⏭️',
  missing: '⚠️',
  gap: '⬜',
  elsewhere: '↗️',
  'n/a': '—',
};

const WORD = {
  success: 'passed',
  failure: 'failed',
  cancelled: 'cancelled',
  skipped: 'skipped',
  missing: 'did not run',
};

const GAP_KINDS = new Set(['gap', 'elsewhere', 'n/a']);

/** Every leg of the matrix with the job that runs it, in declaration order. */
export function legsOf(matrix) {
  const legs = [];
  for (const stage of matrix.stages ?? []) {
    for (const leg of stage.legs ?? []) {
      const matrixJob = leg.matrixJob ?? stage.matrixJob ?? null;
      legs.push({ stage: stage.id, ...leg, job: leg.job ?? matrixJob, matrixJob });
    }
  }
  return legs;
}

/** Problems with the matrix file itself. An empty list means it is well formed. */
export function validate(matrix) {
  const problems = [];
  if (!Array.isArray(matrix.stages) || matrix.stages.length === 0)
    return ['The matrix declares no stages.'];

  const stageIds = new Set();
  for (const stage of matrix.stages) {
    if (!stage.id || !stage.title || !stage.question)
      problems.push(`Stage '${stage.id ?? '?'}' needs an id, a title and the question it answers.`);
    if (stageIds.has(stage.id))
      problems.push(`Stage '${stage.id}' is declared twice.`);
    stageIds.add(stage.id);
    if (!Array.isArray(stage.legs) || stage.legs.length === 0)
      problems.push(`Stage '${stage.id}' has no legs.`);

    const legIds = new Set();
    for (const leg of stage.legs ?? []) {
      if (!leg.id || !leg.title || !leg.proves)
        problems.push(`A leg of '${stage.id}' needs an id, a title and what it proves.`);
      if (legIds.has(leg.id))
        problems.push(`Leg '${stage.id}/${leg.id}' is declared twice.`);
      legIds.add(leg.id);
      if (!leg.job && !leg.matrixJob && !stage.matrixJob)
        problems.push(`Leg '${stage.id}/${leg.id}' names no job.`);
    }

    if (stage.axes) {
      const rows = new Set(stage.axes.rows ?? []);
      const columns = new Set(stage.axes.columns ?? []);
      const filled = new Map();
      for (const leg of stage.legs ?? []) {
        if (!rows.has(leg.row) || !columns.has(leg.column))
          problems.push(`Leg '${stage.id}/${leg.id}' sits outside the ${stage.id} grid (${leg.row} × ${leg.column}).`);
        filled.set(`${leg.row}\u0000${leg.column}`, 'leg');
      }
      for (const gap of (stage.gaps ?? []).filter(item => !item.leg)) {
        const key = `${gap.row}\u0000${gap.column}`;
        if (!rows.has(gap.row) || !columns.has(gap.column))
          problems.push(`A ${stage.id} gap sits outside the grid (${gap.row} × ${gap.column}).`);
        if (filled.get(key) === 'leg')
          problems.push(`${gap.row} × ${gap.column} is both proven by a leg and declared a gap.`);
        if (filled.get(key) === 'gap')
          problems.push(`${gap.row} × ${gap.column} is declared a gap twice.`);
        filled.set(key, 'gap');
      }
      for (const row of rows)
        for (const column of columns)
          if (!filled.has(`${row}\u0000${column}`))
            problems.push(`${row} × ${column} is neither proven by a leg nor declared a gap.`);
    }

    for (const gap of stage.gaps ?? []) {
      if (!GAP_KINDS.has(gap.kind))
        problems.push(`A ${stage.id} gap has kind '${gap.kind}'; use gap, elsewhere or n/a.`);
      if (!gap.reason)
        problems.push(`A ${stage.id} gap has no reason.`);
      if (gap.leg && gap.leg !== 'all' && !legIds.has(gap.leg))
        problems.push(`A ${stage.id} gap names leg '${gap.leg}', which the stage does not declare.`);
    }
  }
  return problems;
}

/** Reads the verdict files the legs uploaded. Later files for the same leg win (a re-run). */
export function readVerdicts(directory) {
  const verdicts = new Map();
  if (!directory || !existsSync(directory))
    return verdicts;
  const files = readdirSync(directory, { recursive: true })
    .map(String)
    .filter(name => name.endsWith('.json'))
    .sort();
  for (const name of files) {
    const verdict = JSON.parse(readFileSync(join(directory, name), 'utf8'));
    if (verdict.stage && verdict.leg)
      verdicts.set(`${verdict.stage}/${verdict.leg}`, verdict);
  }
  return verdicts;
}

/**
 * The status of every leg. A verdict is authoritative; without one, the job result from
 * `needs` says why the leg has nothing to show, and anything else did not run.
 */
export function evaluate(matrix, verdicts, needs = {}) {
  const legs = legsOf(matrix).map(leg => {
    const verdict = verdicts.get(`${leg.stage}/${leg.id}`);
    let status = 'missing';
    if (verdict) status = normalise(verdict.status);
    else if (needs[leg.job]?.result === 'cancelled') status = 'cancelled';
    else if (needs[leg.job]?.result === 'skipped') status = 'skipped';
    return { ...leg, status };
  });
  const unproven = legs.filter(leg => leg.status !== 'success');
  return { legs, unproven, passed: unproven.length === 0 };
}

function normalise(status) {
  const value = String(status ?? '').toLowerCase();
  return value in WORD ? value : 'failure';
}

/** The run summary as GitHub-flavoured markdown. */
export function render(matrix, evaluation) {
  const byStage = new Map();
  for (const leg of evaluation.legs) {
    if (!byStage.has(leg.stage)) byStage.set(leg.stage, []);
    byStage.get(leg.stage).push(leg);
  }
  const gapCount = (matrix.stages ?? []).reduce((count, stage) =>
    count + (stage.gaps ?? []).filter(gap => gap.kind === 'gap').length, 0);

  const lines = ['# Behavior matrix', ''];
  const total = evaluation.legs.length;
  const passed = total - evaluation.unproven.length;
  const named = evaluation.unproven.slice(0, 8).map(leg => `${leg.stage}/${leg.id} ${WORD[leg.status]}`);
  const more = evaluation.unproven.length - named.length;
  lines.push(evaluation.passed
    ? `${ICON.success} **All ${total} legs passed.** ${gapCount} known gap(s) are listed below; nothing proves those cells yet.`
    : `${ICON.failure} **${passed} of ${total} legs passed.** ${named.join(', ')}${more > 0 ? `, and ${more} more` : ''}.`);
  lines.push('');

  for (const stage of matrix.stages) {
    const legs = byStage.get(stage.id) ?? [];
    lines.push(`## ${stage.title}`, '', `_${stage.question}_${stage.gatesOnly ? ` Gates ${stage.gatesOnly} only; nothing else waits for it.` : ''}`, '');
    if (stage.axes) {
      lines.push(...grid(stage, legs), '');
    } else {
      lines.push('| | Leg | Proves |', '|---|---|---|');
      for (const leg of legs)
        lines.push(`| ${ICON[leg.status]} | ${leg.title}${leg.dependency ? ` <br><sub>${leg.dependency}</sub>` : ''} | ${leg.proves} |`);
      lines.push('');
    }
    const gaps = stage.gaps ?? [];
    if (gaps.length > 0) {
      lines.push('<details><summary>' + `${gaps.length} cell(s) not proven here` + '</summary>', '');
      for (const gap of gaps) {
        const where = gap.row ? `${gap.row} × ${gap.column}` : (gap.leg === 'all' ? 'Every leg' : titleOf(legs, gap.leg));
        lines.push(`- ${ICON[gap.kind]} **${where}** (${gap.kind}): ${gap.reason}`);
      }
      lines.push('', '</details>', '');
    }
  }
  return `${lines.join('\n').trimEnd()}\n`;
}

function grid(stage, legs) {
  const { rows, columns } = stage.axes;
  const out = [`| | ${columns.join(' | ')} |`, `|---|${columns.map(() => '---').join('|')}|`];
  for (const row of rows) {
    const cells = columns.map(column => {
      const here = legs.filter(leg => leg.row === row && leg.column === column);
      if (here.length > 0)
        return here.map(leg => `${ICON[leg.status]} ${leg.title}`).join('<br>');
      const gap = (stage.gaps ?? []).find(item => !item.leg && item.row === row && item.column === column);
      return gap ? `${ICON[gap.kind]} ${gap.kind}` : '';
    });
    out.push(`| **${row}** | ${cells.join(' | ')} |`);
  }
  return out;
}

function titleOf(legs, id) {
  return legs.find(leg => leg.id === id)?.title ?? id;
}

function argument(args, name) {
  const index = args.indexOf(`--${name}`);
  return index >= 0 ? args[index + 1] : undefined;
}

function main(args) {
  const [command] = args;
  const matrix = JSON.parse(readFileSync(argument(args, 'matrix') ?? '.github/behavior-matrix.json', 'utf8'));
  const problems = validate(matrix);
  if (problems.length > 0) {
    for (const problem of problems) console.error(problem);
    return 2;
  }
  if (command === 'check')
    return 0;
  if (command !== 'render') {
    console.error('Usage: behavior-matrix.mjs render|check [--matrix file] [--verdicts dir] [--needs file] [--summary file]');
    return 2;
  }

  const needsFile = argument(args, 'needs');
  const needs = needsFile && existsSync(needsFile) ? JSON.parse(readFileSync(needsFile, 'utf8')) : {};
  const evaluation = evaluate(matrix, readVerdicts(argument(args, 'verdicts')), needs);
  const markdown = render(matrix, evaluation);
  const summary = argument(args, 'summary');
  if (summary) appendFileSync(summary, markdown);
  process.stdout.write(markdown);
  return evaluation.passed ? 0 : 1;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1])
  process.exitCode = main(process.argv.slice(2));
