import { unzipSync } from 'fflate';
import type { ChannelRelease, InstalledRelease } from '../models/ChannelRelease';

const storageKey = 'agent-up-active-release';
const maximumArchiveBytes = 25 * 1024 * 1024;
const maximumExpandedBytes = 50 * 1024 * 1024;
const maximumFileCount = 5_000;

export function getInstalledRelease(): InstalledRelease {
  if (typeof localStorage !== 'undefined') {
    const saved = localStorage.getItem(storageKey);
    if (saved) {
      try {
        const release = JSON.parse(saved) as InstalledRelease;
        if (typeof release.channel === 'string' && typeof release.sha === 'string' && typeof release.publishedAt === 'string') {
          return { ...release, sha: release.sha.slice(0, 7) };
        }
      } catch {
        // Invalid updater state falls through to the build identity below.
      }
      localStorage.removeItem(storageKey);
    }
  }
  return {
    channel: process.env.EXPO_PUBLIC_AGENT_UP_CHANNEL ?? 'development',
    sha: process.env.EXPO_PUBLIC_AGENT_UP_SHA ?? 'source',
    publishedAt: process.env.EXPO_PUBLIC_AGENT_UP_PUBLISHED_AT ?? '',
  };
}

export function isUpgrade(current: InstalledRelease, candidate: ChannelRelease): boolean {
  if (current.channel !== candidate.channel) return true;
  const candidateTime = Date.parse(candidate.publishedAt);
  const currentTime = Date.parse(current.publishedAt);
  return Number.isFinite(candidateTime) && (!Number.isFinite(currentTime) || candidateTime > currentTime);
}

export async function installRelease(release: ChannelRelease): Promise<void> {
  if (typeof navigator === 'undefined' || !navigator.serviceWorker?.controller) {
    throw new Error('The installed PWA service worker is not active. Reload and try again.');
  }

  const archive = await fetch(release.assetUrl, {
    headers: { Accept: 'application/octet-stream' },
  });
  if (!archive.ok) throw new Error(`Release download returned ${archive.status}.`);
  const compressed = await archive.arrayBuffer();
  if (compressed.byteLength > maximumArchiveBytes) throw new Error('Release archive exceeds the size limit.');
  await verifyArchiveDigest(compressed, release.archiveSha256);
  const files = parseReleaseZip(new Uint8Array(compressed), release.requiredFiles);

  await new Promise<void>((resolve, reject) => {
    const channel = new MessageChannel();
    channel.port1.onmessage = event => event.data?.ok ? resolve() : reject(new Error(event.data?.error));
    navigator.serviceWorker.controller!.postMessage(
      { type: 'INSTALL_RELEASE', release, files },
      [channel.port2, ...files.map(file => file.body)],
    );
  });
  localStorage.setItem(storageKey, JSON.stringify(release));
  location.reload();
}

export function parseReleaseZip(bytes: Uint8Array, requiredFiles: string[]): { path: string; body: ArrayBuffer }[] {
  const entries = unzipSync(bytes);
  const names = Object.keys(entries);
  if (names.length > maximumFileCount) throw new Error('Release contains too many files.');
  let expandedBytes = 0;
  const files = names.map(name => {
    const path = validateReleasePath(name);
    const contents = entries[name];
    expandedBytes += contents.byteLength;
    if (expandedBytes > maximumExpandedBytes) throw new Error('Expanded release exceeds the size limit.');
    return { path: `/${path}`, body: contents.slice().buffer };
  });
  for (const required of requiredFiles) {
    if (!files.some(file => file.path === `/${required}`)) throw new Error(`Release is missing ${required}.`);
  }
  if (!files.some(file => file.path === '/index.html')) throw new Error('Release is missing index.html.');
  return files;
}

function validateReleasePath(name: string): string {
  const path = name.replace(/^\.\//, '');
  if (!path || path.startsWith('/') || path.includes('\\') || path.split('/').some(part => part === '..' || part === '')) {
    throw new Error(`Release contains an invalid path: ${name}.`);
  }
  return path;
}

async function verifyArchiveDigest(archive: ArrayBuffer, expected: string): Promise<void> {
  if (!/^[0-9a-f]{64}$/i.test(expected)) throw new Error('Release metadata has an invalid archive digest.');
  const digest = await crypto.subtle.digest('SHA-256', archive);
  const actual = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
  if (actual !== expected.toLowerCase()) throw new Error('Release archive integrity check failed.');
}
