import assert from 'node:assert/strict';
import { test } from 'node:test';

import { codeFieldProps } from './AgentSignInFieldProvider';

test('the pasted-code field refuses IME suggestions and autofill', () => {
  const props = codeFieldProps();

  assert.equal(props.autoCorrect, false);
  assert.equal(props.autoComplete, 'off');
  assert.equal(props.spellCheck, false);
  assert.equal(props.importantForAutofill, 'no');
  assert.equal(props.keyboardType, 'visible-password');
});
