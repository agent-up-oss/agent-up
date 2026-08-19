export type ChannelRelease = {
  channel: string;
  sha: string;
  publishedAt: string;
  assetUrl: string;
  archiveSha256: string;
  requiredFiles: string[];
};

export type InstalledRelease = Pick<ChannelRelease, 'channel' | 'sha' | 'publishedAt'>;
