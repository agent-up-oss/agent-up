import assert from 'node:assert/strict';
import test from 'node:test';
import { agentUpTheme, auBox, auText } from '../dist/native/index.js';
import catalog from '../dist/web/catalog.json' with { type: 'json' };
import { detectLanguage, tokenizeLine } from '../src/syntax/highlight.mjs';

test('file viewer catalog owns inspection chrome, line kinds, and syntax tokens', () => {
  const surface = catalog.surfaces.find(item => item.id === 'file-viewer');
  assert.ok(surface, 'file-viewer surface is missing from the catalog');
  const ids = new Set(surface.components.map(item => item.id));
  for (const id of [
    'file-viewer', 'file-viewer-header', 'file-viewer-path', 'file-viewer-status',
    'file-viewer-nav', 'file-viewer-jump', 'file-viewer-line', 'file-viewer-line-added',
    'file-viewer-line-deleted', 'file-viewer-line-hunk', 'file-viewer-line-current',
    'file-viewer-gutter', 'syntax-keyword', 'syntax-string', 'syntax-comment',
  ]) {
    assert.ok(ids.has(id), `catalog is missing ${id}`);
  }
  assert.equal(agentUpTheme.components.fileViewer.backgroundColor, agentUpTheme.colors.surfaceOverlay);
  assert.equal(agentUpTheme.components.fileViewerLineAdded.backgroundColor, agentUpTheme.colors.surfaceSelectedSoft);
  assert.equal(agentUpTheme.components.fileViewerLineDeleted.backgroundColor, agentUpTheme.colors.surfaceDanger);
  assert.equal(agentUpTheme.components.fileViewerLineCurrent.borderLeftColor, agentUpTheme.colors.accentLine);
  assert.equal(agentUpTheme.components.syntaxKeyword.color, agentUpTheme.colors.textInfo);
  assert.equal(agentUpTheme.components.syntaxString.color, agentUpTheme.colors.textWarning);
  assert.equal(agentUpTheme.components.syntaxComment.color, agentUpTheme.colors.textMuted);
  assert.equal(auText('syntaxComment').fontStyle, 'italic');
  assert.equal(auBox('fileViewerLineCurrent').backgroundColor, agentUpTheme.colors.surfaceSelected);
});

test('Mobile and Desktop bind the file viewer instead of a plain text dump', async () => {
  const { readFile } = await import('node:fs/promises');
  const { resolve } = await import('node:path');
  const repository = resolve(new URL('../..', import.meta.url).pathname);
  const axaml = await readFile(resolve(repository, 'AgentUp.Desktop/Features/Workspaces/Views/MainWindow.axaml'), 'utf8');
  assert.match(axaml, /Classes="au-overlay-panel au-file-viewer"/);
  assert.match(axaml, /x:Name="GitFileDiffLines"/);
  assert.doesNotMatch(axaml, /x:Name="GitFileDiffContent"/);
  const mobile = await readFile(resolve(repository, 'AgentUp.Mobile/src/features/git/components/GitFileViewer.tsx'), 'utf8');
  assert.match(mobile, /auBox\('fileViewer'\)/);
  assert.match(mobile, /FlatList/);
  const panel = await readFile(resolve(repository, 'AgentUp.Mobile/src/features/git/components/GitChangesPanel.tsx'), 'utf8');
  assert.match(panel, /<GitFileViewer/);
  assert.doesNotMatch(panel, /diffText/);
});

test('shared highlighter maps extensions and tokenizes keywords without a second palette', () => {
  assert.equal(detectLanguage('AgentChatScreen.tsx'), 'typescript');
  assert.equal(detectLanguage('GitChangesController.cs'), 'csharp');
  assert.equal(detectLanguage('agent-up.json'), 'json');
  assert.equal(detectLanguage('README'), 'plaintext');

  const tokens = tokenizeLine('const path = "ws-1"; // Server-owned', 'typescript');
  assert.ok(tokens.some(token => token.kind === 'keyword' && token.text === 'const'));
  assert.ok(tokens.some(token => token.kind === 'string' && token.text.includes('ws-1')));
  assert.ok(tokens.some(token => token.kind === 'comment' && token.text.includes('Server-owned')));

  const csharp = tokenizeLine('public sealed class GitFileDiff', 'csharp');
  assert.ok(csharp.some(token => token.kind === 'keyword' && token.text === 'class'));
  assert.ok(csharp.some(token => token.kind === 'keyword' && token.text === 'public'));
  assert.ok(csharp.map(token => token.text).join('').includes('GitFileDiff'));
});
