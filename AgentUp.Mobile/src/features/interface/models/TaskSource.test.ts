import assert from 'node:assert/strict';
import test from 'node:test';
import { canSaveSource, sourceLabel, splitRepository, type TaskSource } from './TaskSource';

function source(partial: Partial<TaskSource> = {}): TaskSource {
  return {
    moduleId: 'github',
    owner: 'agent-up-oss',
    repository: 'agent-up',
    configured: true,
    connected: false,
    canConnect: true,
    detail: '',
    modules: [],
    ...partial,
  };
}

test('a source reads as the repository it names', () => {
  assert.equal(sourceLabel(source()), 'agent-up-oss/agent-up');
  assert.equal(sourceLabel(source({ owner: null, repository: null })), 'Not set');
});

test('a half-named source cannot be saved', () => {
  assert.equal(canSaveSource('github', 'agent-up-oss', 'agent-up'), true);
  assert.equal(canSaveSource('', 'agent-up-oss', 'agent-up'), false);
  assert.equal(canSaveSource('github', '  ', 'agent-up'), false);
  assert.equal(canSaveSource('github', 'agent-up-oss', ''), false);
});

test('a repository is taken however the reader happened to write it', () => {
  // All of these name the same repository, and people reach for all of them.
  const expected = { owner: 'agent-up-oss', repository: 'agent-up' };

  assert.deepEqual(splitRepository('agent-up-oss/agent-up'), expected);
  assert.deepEqual(splitRepository('  agent-up-oss/agent-up  '), expected);
  assert.deepEqual(splitRepository('https://github.com/agent-up-oss/agent-up'), expected);
  assert.deepEqual(splitRepository('https://github.com/agent-up-oss/agent-up.git'), expected);
  assert.deepEqual(splitRepository('https://github.com/agent-up-oss/agent-up/'), expected);
});

test('something that is not a repository is refused rather than half-read', () => {
  assert.equal(splitRepository('agent-up'), null);
  assert.equal(splitRepository(''), null);
  assert.equal(splitRepository('   '), null);
  assert.equal(splitRepository('/'), null);
});
