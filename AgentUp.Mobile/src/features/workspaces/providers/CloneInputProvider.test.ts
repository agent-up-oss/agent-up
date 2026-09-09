import assert from 'node:assert/strict';
import { test } from 'node:test';
import { canCloneWorkspace } from './CloneInputProvider';

test('clone is offered once a repository and a branch are entered', () => {
  assert.equal(canCloneWorkspace('https://example.test/acme/widgets.git', 'main'), true);
});

test('clone is refused for blank or whitespace-only input', () => {
  assert.equal(canCloneWorkspace('', 'main'), false);
  assert.equal(canCloneWorkspace('   ', 'main'), false);
  assert.equal(canCloneWorkspace('https://example.test/acme/widgets.git', ''), false);
  assert.equal(canCloneWorkspace('https://example.test/acme/widgets.git', '\t \n'), false);
  assert.equal(canCloneWorkspace('  ', '  '), false);
});

test('clone is offered for values that are non-empty once trimmed', () => {
  assert.equal(canCloneWorkspace('  https://example.test/acme/widgets.git  ', ' feature/login '), true);
});
