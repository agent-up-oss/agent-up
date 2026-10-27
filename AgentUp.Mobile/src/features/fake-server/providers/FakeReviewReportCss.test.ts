import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import compiled from '../../../../../AgentUp.FakeServer/agent-up.css.json';
import { fakeReviewReportStyleTag } from './FakeReviewReportCss';

const here = dirname(fileURLToPath(import.meta.url));
const designSystemCss = resolve(here, '../../../../../AgentUp.DesignSystem/dist/web/agent-up.css');

test('demo report css matches the compiled design-system stylesheet', () => {
  assert.equal(compiled.css, readFileSync(designSystemCss, 'utf8'));
});

test('demo report style tag inlines canvas tokens and report layout', () => {
  const tag = fakeReviewReportStyleTag();
  assert.equal(tag.includes('<style>'), true);
  assert.equal(tag.includes('--au-color-canvas'), true);
  assert.equal(tag.includes('.au-report'), true);
});
