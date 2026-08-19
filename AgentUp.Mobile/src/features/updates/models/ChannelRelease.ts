export type ChannelRelease = {
  channel: string;
  sha: string;
  publishedAt: string;
  assetUrl: string;
};

export type InstalledRelease = Pick<ChannelRelease, 'channel' | 'sha' | 'publishedAt'>;
