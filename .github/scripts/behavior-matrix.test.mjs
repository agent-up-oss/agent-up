import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import { evaluate, legsOf, readVerdicts, render, validate } from './behavior-matrix.mjs';

const repositoryMatrix = JSON.parse(readFileSync(fileURLToPath(new URL('../behavior-matrix.json', import.meta.url)), 'utf8'));

const sample = () => ({
  stages: [
    {
      id: 'source', title: 'Source', question: 'Does the code hold?', gatesOnly: 'release',
      legs: [{ id: 'dotnet', job: 'source-dotnet', title: '.NET suites', proves: 'Every suite.' }],
    },
    {
      id: 'frontend', title: 'Frontend', question: 'Same everywhere?',
      axes: { rows: ['Desktop', 'Mobile'], columns: ['Server', 'Demo'] },
      legs: [
        { id: 'journeys-desktop', matrixJob: 'journeys', title: 'Journeys · Desktop', row: 'Desktop', column: 'Server', proves: 'Flows.' },
        { id: 'showcase-desktop', matrixJob: 'showcase', title: 'Showcase · Desktop', row: 'Desktop', column: 'Demo', proves: 'Screens.' },
        { id: 'journeys-mobile', matrixJob: 'journeys', title: 'Journeys · Mobile', row: 'Mobile', column: 'Server', proves: 'Flows.' },
      ],
      gaps: [{ row: 'Mobile', column: 'Demo', kind: 'gap', reason: 'Nothing captures it.' }],
    },
  ],
});

const verdicts = entries => new Map(entries.map(([stage, leg, status]) => [`${stage}/${leg}`, { stage, leg, status }]));

test('the repository matrix is well formed', () => {
  assert.deepEqual(validate(repositoryMatrix), []);
});

test('every leg of the repository matrix names the job that runs it', () => {
  for (const leg of legsOf(repositoryMatrix))
    assert.ok(leg.job, `${leg.stage}/${leg.id} names no job`);
});

test('a run passes only when every declared leg recorded success', () => {
  const all = verdicts([
    ['source', 'dotnet', 'success'],
    ['frontend', 'journeys-desktop', 'success'],
    ['frontend', 'showcase-desktop', 'success'],
    ['frontend', 'journeys-mobile', 'success'],
  ]);
  assert.equal(evaluate(sample(), all).passed, true);
});

test('a leg that recorded nothing did not run, and fails the gate like a failure', () => {
  const evaluation = evaluate(sample(), verdicts([
    ['source', 'dotnet', 'success'],
    ['frontend', 'journeys-desktop', 'success'],
    ['frontend', 'showcase-desktop', 'success'],
  ]));
  assert.equal(evaluation.passed, false);
  assert.deepEqual(evaluation.unproven.map(leg => [leg.id, leg.status]), [['journeys-mobile', 'missing']]);
});

test('a job the workflow skipped or cancelled says so instead of did not run', () => {
  const evaluation = evaluate(sample(), verdicts([['source', 'dotnet', 'success']]), {
    journeys: { result: 'cancelled' },
    showcase: { result: 'skipped' },
  });
  const status = Object.fromEntries(evaluation.legs.map(leg => [leg.id, leg.status]));
  assert.equal(status['journeys-desktop'], 'cancelled');
  assert.equal(status['showcase-desktop'], 'skipped');
  assert.equal(evaluation.passed, false);
});

test('an unknown status reads as a failure rather than a pass', () => {
  const evaluation = evaluate(sample(), verdicts([['source', 'dotnet', 'weird']]));
  assert.equal(evaluation.legs.find(leg => leg.id === 'dotnet').status, 'failure');
});

test('the summary draws the grid with each cell proven, failed or declared a gap', () => {
  const markdown = render(sample(), evaluate(sample(), verdicts([
    ['source', 'dotnet', 'success'],
    ['frontend', 'journeys-desktop', 'failure'],
    ['frontend', 'showcase-desktop', 'success'],
    ['frontend', 'journeys-mobile', 'success'],
  ])));
  assert.match(markdown, /\| \*\*Desktop\*\* \| ❌ Journeys · Desktop \| ✅ Showcase · Desktop \|/);
  assert.match(markdown, /\| \*\*Mobile\*\* \| ✅ Journeys · Mobile \| ⬜ gap \|/);
  assert.match(markdown, /Mobile × Demo\*\* \(gap\): Nothing captures it\./);
  assert.match(markdown, /3 of 4 legs passed\.\*\* frontend\/journeys-desktop failed/);
  assert.match(markdown, /Gates release only/);
});

test('a grid cell with neither a leg nor a gap is a malformed matrix', () => {
  const matrix = sample();
  matrix.stages[1].gaps = [];
  assert.deepEqual(validate(matrix), ['Mobile × Demo is neither proven by a leg nor declared a gap.']);
});

test('a cell cannot be both proven and a gap', () => {
  const matrix = sample();
  matrix.stages[1].gaps.push({ row: 'Desktop', column: 'Server', kind: 'gap', reason: 'Twice.' });
  assert.ok(validate(matrix).includes('Desktop × Server is both proven by a leg and declared a gap.'));
});

test('a gap about a leg sits beside the grid rather than in a cell', () => {
  const matrix = sample();
  matrix.stages[1].gaps.push({ leg: 'showcase-desktop', kind: 'gap', reason: 'Not compared.' });
  assert.deepEqual(validate(matrix), []);
  const markdown = render(matrix, evaluate(matrix, new Map()));
  assert.match(markdown, /Showcase · Desktop\*\* \(gap\): Not compared\./);
});

test('a gap about a leg names a leg the stage declares', () => {
  const matrix = sample();
  matrix.stages[1].gaps.push({ leg: 'nope', kind: 'gap', reason: 'Unknown.' });
  assert.ok(validate(matrix).some(problem => /names leg 'nope'/.test(problem)));
});

test('a gap must say what kind it is and why', () => {
  const matrix = sample();
  matrix.stages[1].gaps[0] = { row: 'Mobile', column: 'Demo', kind: 'later' };
  const problems = validate(matrix);
  assert.ok(problems.some(problem => /kind 'later'/.test(problem)));
  assert.ok(problems.some(problem => /has no reason/.test(problem)));
});

test('verdict files are read recursively and the later file for a leg wins', () => {
  const directory = mkdtempSync(join(tmpdir(), 'verdicts-'));
  mkdirSync(join(directory, 'verdict-frontend--journeys-mobile'));
  writeFileSync(join(directory, 'verdict-frontend--journeys-mobile', 'a.json'), JSON.stringify({ stage: 'frontend', leg: 'journeys-mobile', status: 'failure' }));
  writeFileSync(join(directory, 'verdict-frontend--journeys-mobile', 'b.json'), JSON.stringify({ stage: 'frontend', leg: 'journeys-mobile', status: 'success' }));
  assert.equal(readVerdicts(directory).get('frontend/journeys-mobile').status, 'success');
  assert.equal(readVerdicts(join(directory, 'absent')).size, 0);
});

test('a skipped verdict passes the gate only for a leg that declares when it skips', () => {
  const matrix = sample();
  const everyLeg = status => verdicts([
    ['source', 'dotnet', 'success'],
    ['frontend', 'journeys-desktop', 'success'],
    ['frontend', 'showcase-desktop', 'success'],
    ['frontend', 'journeys-mobile', status],
  ]);
  assert.equal(evaluate(matrix, everyLeg('skipped')).passed, false);

  matrix.stages[1].legs[2].skippable = 'Without signing secrets.';
  const evaluation = evaluate(matrix, everyLeg('skipped'));
  assert.equal(evaluation.passed, true);
  assert.deepEqual(evaluation.skipped.map(leg => leg.id), ['journeys-mobile']);
  assert.match(render(matrix, evaluation), /3 of 4 legs passed\.\*\* 1 skipped as declared\./);
  assert.match(render(matrix, evaluation), /Journeys · Mobile\*\* skipped as declared: Without signing secrets\./);
});

test('a declared skippable leg that recorded nothing still fails the gate', () => {
  const matrix = sample();
  matrix.stages[1].legs[2].skippable = 'Without signing secrets.';
  const evaluation = evaluate(matrix, verdicts([
    ['source', 'dotnet', 'success'],
    ['frontend', 'journeys-desktop', 'success'],
    ['frontend', 'showcase-desktop', 'success'],
  ]), { journeys: { result: 'skipped' } });
  assert.equal(evaluation.passed, false);
});

test('skippable must say when the leg skips', () => {
  const matrix = sample();
  matrix.stages[1].legs[2].skippable = ' ';
  assert.match(validate(matrix).join('\n'), /declares skippable without saying when it skips/);
});
