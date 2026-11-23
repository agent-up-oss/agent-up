#!/usr/bin/env node
// The record a Mobile store leg leaves beside the binary it built, and that Mobile release reads,
// so the release publishes exactly what the matrix proved: the same file, version and build
// number, never a second build.
//
//   node mobile-release-manifest.mjs write <release.json> <android|ios> <version> <version-code> <source-tag> <file>
//   node mobile-release-manifest.mjs read <release.json> <android|ios>   (key=value lines for GITHUB_OUTPUT)

import { readFileSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const PLATFORMS = { android: '.aab', ios: '.ipa' };

export function manifest(platform, version, versionCode, sourceTag, file) {
  if (!(platform in PLATFORMS))
    throw new Error(`Unknown store platform '${platform}'.`);
  if (!/^\d+\.\d+\.\d+$/.test(version))
    throw new Error(`Version '${version}' is not X.Y.Z.`);
  if (!/^[1-9][0-9]*$/.test(String(versionCode)))
    throw new Error(`Build number '${versionCode}' is not a positive integer.`);
  if (!sourceTag)
    throw new Error('The source tag is empty.');
  if (basename(file) !== file || !file.endsWith(PLATFORMS[platform]))
    throw new Error(`'${file}' is not a ${PLATFORMS[platform]} file name.`);
  return { platform, version, versionCode: String(versionCode), sourceTag, file, checksum: `${file}.sha256` };
}

/** Reads a manifest for the platform a release leg ships, and the paths it names beside it. */
export function readManifest(text, platform, directory) {
  const parsed = JSON.parse(text);
  if (parsed.platform !== platform)
    throw new Error(`The artifact was built for ${parsed.platform}, not ${platform}.`);
  const checked = manifest(parsed.platform, parsed.version, parsed.versionCode, parsed.sourceTag, parsed.file);
  return { ...checked, path: join(directory, checked.file), checksumPath: join(directory, checked.checksum) };
}

function main(argv) {
  const [command, path, platform, ...rest] = argv;
  if (command === 'write') {
    const [version, versionCode, sourceTag, file] = rest;
    writeFileSync(path, `${JSON.stringify(manifest(platform, version, versionCode, sourceTag, file), null, 2)}\n`);
    return;
  }
  if (command === 'read') {
    const read = readManifest(readFileSync(path, 'utf8'), platform, dirname(path));
    for (const key of ['version', 'versionCode', 'sourceTag', 'path', 'checksumPath'])
      console.log(`${key}=${read[key]}`);
    return;
  }
  throw new Error('Usage: mobile-release-manifest.mjs write|read <release.json> <android|ios> ...');
}

if (process.argv[1] === fileURLToPath(import.meta.url))
  main(process.argv.slice(2));
