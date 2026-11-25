import assert from 'node:assert/strict';
import { test } from 'node:test';
import { forgeHostLabel, memberInitials, repositoryId } from './TaskSource';

test('a repository row names the host and the repository together', () => {
  assert.equal(repositoryId({ moduleId: 'github', owner: 'example', name: 'repository' }), 'github/example/repository');
  assert.equal(forgeHostLabel('github'), 'GitHub');
  assert.equal(forgeHostLabel('forgejo'), 'Forgejo');
});

test('member initials take the first letter of each word, or the first two of one', () => {
  assert.equal(memberInitials('Bob'), 'BO');
  assert.equal(memberInitials('Ann Lee'), 'AL');
  assert.equal(memberInitials('hector'), 'HE');
});
