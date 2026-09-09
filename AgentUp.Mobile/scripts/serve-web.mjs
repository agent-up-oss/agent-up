import http from 'node:http';
import { existsSync, statSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { dirname, extname, isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveMobilePort, waitForPortAvailable } from './mobile-port.mjs';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const defaultRoot = join(scriptDirectory, '..', 'dist');

const contentTypes = {
  '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8',
  '.ico': 'image/x-icon',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.txt': 'text/plain; charset=utf-8',
  '.webmanifest': 'application/manifest+json',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.webp': 'image/webp',
};

export function resolveDistFile(root, urlPath) {
  const pathname = decodeURIComponent(new URL(urlPath, 'http://localhost').pathname);
  const relativePath = pathname === '/' || pathname === '' ? 'index.html' : pathname.replace(/^\//, '');
  const candidates = [
    relativePath,
    join(relativePath, 'index.html'),
    relativePath.endsWith('.html') ? null : `${relativePath}.html`,
  ].filter(Boolean);

  for (const candidate of candidates) {
    const absolute = resolve(root, candidate);
    if (!isUnderRoot(root, absolute) || !isReadableFile(absolute)) continue;
    return absolute;
  }

  return null;
}

export function contentTypeFor(filePath) {
  return contentTypes[extname(filePath).toLowerCase()] ?? 'application/octet-stream';
}

function isUnderRoot(root, filePath) {
  const rel = relative(root, filePath);
  return rel !== '' && !rel.startsWith('..') && !isAbsolute(rel);
}

function isReadableFile(filePath) {
  try {
    return statSync(filePath).isFile();
  } catch {
    return false;
  }
}

export async function startStaticWebServer(options = {}) {
  const {
    root = defaultRoot,
    host = '127.0.0.1',
    port = resolveMobilePort(process.env.WEB_PORT),
    waitForPort = waitForPortAvailable,
    log = console.log,
  } = options;

  if (!existsSync(join(root, 'index.html'))) {
    throw new Error('Static web build is missing. Run npm run build:web before serving.');
  }

  await waitForPort(port);

  const server = http.createServer(async (request, response) => {
    const filePath = resolveDistFile(root, request.url ?? '/');
    if (!filePath) {
      response.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' });
      response.end('Not found');
      return;
    }

    try {
      const body = await readFile(filePath);
      response.writeHead(200, { 'content-type': contentTypeFor(filePath) });
      response.end(body);
    } catch (error) {
      log(`[agent-up] failed to read ${filePath}:`, error);
      response.writeHead(500, { 'content-type': 'text/plain; charset=utf-8' });
      response.end('Failed to read file');
    }
  });

  await new Promise((resolvePromise, reject) => {
    server.once('error', reject);
    server.listen(port, host, resolvePromise);
  });

  log(`Agent-Up Mobile ready at http://${host}:${port}`);
  return server;
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1]);
if (isMain) {
  const server = await startStaticWebServer();
  server.on('close', () => process.exit(0));
}
