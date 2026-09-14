import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import { extname, join, normalize, resolve, sep } from 'node:path';

import { freePort } from './stack.mjs';

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.webp': 'image/webp',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
};

/**
 * Serves the Metro web export over real HTTP.
 *
 * The installable client has to be exercised the way it is actually delivered, as static files
 * from an origin, rather than through a dev server that resolves modules differently.
 */
export async function startStaticServer(exportDir) {
  const root = resolve(exportDir);
  const port = await freePort();

  const server = createServer(async (request, response) => {
    const requested = decodeURIComponent((request.url ?? '/').split('?')[0]);
    const file = await resolveFile(root, requested);
    if (!file) {
      response.writeHead(404).end('Not found');
      return;
    }

    response.writeHead(200, { 'content-type': TYPES[extname(file)] ?? 'application/octet-stream' });
    createReadStream(file).pipe(response);
  });

  await new Promise(resolve => server.listen(port, '127.0.0.1', resolve));

  return {
    url: `http://localhost:${port}`,
    dispose: () => new Promise(resolve => server.close(resolve)),
  };
}

async function resolveFile(root, requested) {
  const candidate = normalize(join(root, requested));
  // Never serve outside the export: a traversal here would silently read the repository.
  if (candidate !== root && !candidate.startsWith(root + sep)) return null;

  const direct = await fileOrNull(candidate);
  if (direct) return direct;

  const index = await fileOrNull(join(candidate, 'index.html'));
  if (index) return index;

  // The client is a single-page app, so unknown paths fall back to its entry document.
  return fileOrNull(join(root, 'index.html'));
}

async function fileOrNull(path) {
  try {
    return (await stat(path)).isFile() ? path : null;
  } catch {
    return null;
  }
}
