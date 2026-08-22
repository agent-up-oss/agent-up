import type { ChannelRelease } from '../models/ChannelRelease';

const releasesUrl = 'https://api.github.com/repos/agent-up-oss/agent-up/releases?per_page=100';
const tagPattern = /^rc-(\d+)-([0-9a-f]{7,40})$/i;

type GitHubRelease = {
  tag_name: string;
  prerelease: boolean;
  published_at: string;
  assets: { id: number; name: string }[];
};

type ReleaseMetadata = {
  channel: string;
  sha: string;
  publishedAt: string;
  archiveSha256: string;
  requiredFiles: string[];
};

export async function getChannelReleases(): Promise<ChannelRelease[]> {
  const response = await fetch(releasesUrl, { headers: { Accept: 'application/vnd.github+json' } });
  if (!response.ok) {
    throw new Error(`GitHub returned ${response.status}.`);
  }

  const releases = (await response.json()) as GitHubRelease[];
  const candidates = releases.flatMap(release => {
    const match = tagPattern.exec(release.tag_name);
    const archive = release.assets.find(candidate => candidate.name === 'agent-up-mobile-web.zip');
    const metadata = release.assets.find(candidate => candidate.name === 'release.json');
    if (!release.prerelease || !match || !archive || !metadata) return [];
    return [{ release, match, archive, metadata }];
  });

  const channelReleases = await Promise.all(candidates.map(async ({ release, match, archive, metadata }) => {
    const details = await fetchMetadata(metadata.id);
    const channel = match[1];
    const sha = match[2].slice(0, 7).toLowerCase();
    if (details.channel !== channel || details.sha.toLowerCase() !== sha) {
      throw new Error(`Release metadata does not match ${release.tag_name}.`);
    }
    return {
      channel,
      sha,
      publishedAt: details.publishedAt || release.published_at,
      assetUrl: assetUrl(archive.id),
      archiveSha256: details.archiveSha256.toLowerCase(),
      requiredFiles: details.requiredFiles,
    };
  }));

  return channelReleases.sort((left, right) => Date.parse(right.publishedAt) - Date.parse(left.publishedAt));
}

async function fetchMetadata(assetId: number): Promise<ReleaseMetadata> {
  const response = await fetch(assetUrl(assetId), { headers: { Accept: 'application/octet-stream' } });
  if (!response.ok) throw new Error(`Release metadata returned ${response.status}.`);
  return response.json() as Promise<ReleaseMetadata>;
}

function assetUrl(assetId: number): string {
  return `https://api.github.com/repos/agent-up-oss/agent-up/releases/assets/${assetId}`;
}
