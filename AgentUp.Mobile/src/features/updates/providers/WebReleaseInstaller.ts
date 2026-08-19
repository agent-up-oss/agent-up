import type { ChannelRelease, InstalledRelease } from '../models/ChannelRelease';

const storageKey = 'agent-up-active-release';

export function getInstalledRelease(): InstalledRelease {
  if (typeof localStorage !== 'undefined') {
    const saved = localStorage.getItem(storageKey);
    if (saved) return JSON.parse(saved) as InstalledRelease;
  }
  return {
    channel: process.env.EXPO_PUBLIC_AGENT_UP_CHANNEL ?? 'development',
    sha: process.env.EXPO_PUBLIC_AGENT_UP_SHA ?? 'source',
    publishedAt: process.env.EXPO_PUBLIC_AGENT_UP_PUBLISHED_AT ?? '',
  };
}

export function isUpgrade(current: InstalledRelease, candidate: ChannelRelease): boolean {
  return current.channel !== candidate.channel || candidate.publishedAt > current.publishedAt;
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
  const stream = new Blob([compressed]).stream().pipeThrough(new DecompressionStream('gzip'));
  const files = parseTar(new Uint8Array(await new Response(stream).arrayBuffer()));

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

function parseTar(bytes: Uint8Array): { path: string; body: ArrayBuffer }[] {
  const files: { path: string; body: ArrayBuffer }[] = [];
  const decoder = new TextDecoder();
  for (let offset = 0; offset + 512 <= bytes.length;) {
    const header = bytes.subarray(offset, offset + 512);
    const name = decoder.decode(header.subarray(0, 100)).replace(/\0.*$/, '');
    if (!name) break;
    const size = parseInt(decoder.decode(header.subarray(124, 136)).replace(/\0.*$/, '').trim() || '0', 8);
    const type = header[156];
    offset += 512;
    if ((type === 0 || type === 48) && !name.endsWith('/')) {
      const body = bytes.slice(offset, offset + size).buffer;
      files.push({ path: `/${name.replace(/^\.\//, '')}`, body });
    }
    offset += Math.ceil(size / 512) * 512;
  }
  if (!files.some(file => file.path === '/index.html')) throw new Error('Release is missing index.html.');
  return files;
}
