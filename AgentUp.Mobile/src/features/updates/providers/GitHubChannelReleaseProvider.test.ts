import assert from 'node:assert/strict';
import test from 'node:test';
import { getChannelReleases } from './GitHubChannelReleaseProvider';

test('channel releases are sorted newest first independently of API order', async () => {
  const releases = [
    githubRelease('rc-235-1111111', '2026-08-18T00:00:00Z', 1, 2),
    githubRelease('rc-235-2222222', '2026-08-19T00:00:00Z', 3, 4),
  ];
  const metadata = new Map<number, object>([
    [2, releaseMetadata('1111111', '2026-08-18T00:00:00Z')],
    [4, releaseMetadata('2222222', '2026-08-19T00:00:00Z')],
  ]);
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async input => {
    const url = String(input);
    if (url.includes('/releases?')) return Response.json(releases);
    const assetId = Number(url.split('/').pop());
    return Response.json(metadata.get(assetId));
  };
  try {
    const result = await getChannelReleases();
    assert.deepEqual(result.map(candidate => candidate.sha), ['2222222', '1111111']);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function githubRelease(tag: string, publishedAt: string, archiveId: number, metadataId: number) {
  return {
    tag_name: tag,
    prerelease: true,
    published_at: publishedAt,
    assets: [
      { id: archiveId, name: 'agent-up-mobile-web.zip' },
      { id: metadataId, name: 'release.json' },
    ],
  };
}

function releaseMetadata(sha: string, publishedAt: string) {
  return {
    channel: '235', sha, publishedAt, archiveSha256: 'a'.repeat(64), requiredFiles: ['index.html'],
  };
}
