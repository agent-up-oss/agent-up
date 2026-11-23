import assert from 'node:assert/strict';
import { test } from 'node:test';
import { forgeHostLabel, repositoryId } from './TaskSource';

test('a repository row names the host and the repository together', () => {
  assert.equal(repositoryId({ moduleId: 'github', owner: 'example', name: 'repository' }), 'github/example/repository');
  assert.equal(forgeHostLabel('github'), 'GitHub');
  assert.equal(forgeHostLabel('forgejo'), 'Forgejo');
});
