import { readdirSync, writeFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const manifestFile = 'bootstrap-manifest.json';
const excludedFiles = new Set([manifestFile, 'sw.js']);

export function createBootstrapManifest(outputDirectory, channel, sha) {
  const files = listFiles(outputDirectory)
    .filter(path => !excludedFiles.has(path))
    .map(path => `/${path}`);

  return {
    cacheName: `agent-up-release-bootstrap-${safeName(channel)}-${safeName(sha)}`,
    files,
  };
}

export function writeBootstrapManifest(outputDirectory, channel, sha) {
  const manifest = createBootstrapManifest(outputDirectory, channel, sha);
  writeFileSync(join(outputDirectory, manifestFile), `${JSON.stringify(manifest)}\n`);
}

function listFiles(root, directory = root) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return listFiles(root, path);
    return [relative(root, path).split(sep).join('/')];
  }).sort();
}

function safeName(value) {
  return value.replace(/[^a-zA-Z0-9_-]/g, '-');
}
