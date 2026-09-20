import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import { extname, join, resolve as resolvePath, sep } from 'node:path';

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
};

export async function startStaticServer(exportDir) {
  const root = resolvePath(exportDir);
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

  await new Promise((resolve, reject) => {
    server.listen(0, '127.0.0.1', resolve);
    server.once('error', reject);
  });
  const address = server.address();

  return {
    url: `http://127.0.0.1:${address.port}`,
    dispose: () => new Promise(resolve => server.close(resolve)),
  };
}

async function resolveFile(root, requested) {
  const candidate = join(root, ...requested.split('/').filter(segment => segment.length > 0 && segment !== '.' && segment !== '..'));
  for (const path of [candidate, join(candidate, 'index.html'), join(root, 'index.html')]) {
    try {
      if (path.startsWith(root + sep) && (await stat(path)).isFile()) return path;
    } catch {
      // try the next fallback
    }
  }
  return null;
}
