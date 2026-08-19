import type { ChannelRelease } from '../models/ChannelRelease';

const releasesUrl = 'https://api.github.com/repos/agent-up-oss/agent-up/releases?per_page=100';
const tagPattern = /^rc-(\d+)-([0-9a-f]{7,40})$/i;

type GitHubRelease = {
  tag_name: string;
  prerelease: boolean;
  published_at: string;
  assets: { id: number; name: string }[];
};

export async function getChannelReleases(): Promise<ChannelRelease[]> {
  const response = await fetch(releasesUrl, { headers: { Accept: 'application/vnd.github+json' } });
  if (!response.ok) {
    throw new Error(`GitHub returned ${response.status}.`);
  }

  const releases = (await response.json()) as GitHubRelease[];
  return releases.flatMap(release => {
    const match = tagPattern.exec(release.tag_name);
    const asset = release.assets.find(candidate => candidate.name === 'agent-up-mobile-web.tar.gz');
    if (!release.prerelease || !match || !asset) return [];
    return [{
      channel: match[1],
      sha: match[2].slice(0, 7).toLowerCase(),
      publishedAt: release.published_at,
      assetUrl: `https://api.github.com/repos/agent-up-oss/agent-up/releases/assets/${asset.id}`,
    }];
  });
}
