import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  decisionExportFileName,
  exportedCount,
  saveDecisionExport,
  type DecisionExportSaver,
} from './DecisionExportProvider';

test('an export is named for the day it was taken, so several of them sort together', () => {
  assert.equal(
    decisionExportFileName(new Date('2026-03-04T09:00:00Z')),
    'agent-up-product-decisions-2026-03-04.jsonl',
  );
});

test('an export carries one decision per line', () => {
  assert.equal(exportedCount('{"id":"a"}\n{"id":"b"}\n'), 2);
});

test('an empty export carries nothing rather than one blank line', () => {
  assert.equal(exportedCount('\n\n'), 0);
});

test('the export is handed to whatever can save it here', async () => {
  const saved: { jsonl: string; fileName: string }[] = [];
  const saver: DecisionExportSaver = {
    async save(jsonl, fileName) { saved.push({ jsonl, fileName }); },
  };

  await saveDecisionExport('{"id":"a"}\n', 'decisions.jsonl', saver);

  assert.deepEqual(saved, [{ jsonl: '{"id":"a"}\n', fileName: 'decisions.jsonl' }]);
});
