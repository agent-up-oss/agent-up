import assert from 'node:assert/strict';
import { test } from 'node:test';
import { reviewReportFileName, saveReviewReport } from './ReviewReportExportProvider';

test('reviewReportFileName slugifies the workspace display name', () => {
  assert.equal(reviewReportFileName('Harbor Shop'), 'agent-up-report-Harbor-Shop.html');
  assert.equal(reviewReportFileName('   '), 'agent-up-report.html');
});

test('saveReviewReport delegates to the injected saver', async () => {
  const saved: { html?: string; fileName?: string } = {};

  await saveReviewReport('<html>Shop</html>', 'agent-up-report-Shop.html', {
    async save(html, fileName) {
      saved.html = html;
      saved.fileName = fileName;
    },
  });

  assert.equal(saved.html, '<html>Shop</html>');
  assert.equal(saved.fileName, 'agent-up-report-Shop.html');
});
