'use strict';

const { execFileSync } = require('child_process');
const fs = require('fs');

const PRODUCT_TAG = /^v(\d+\.\d+\.\d+)$/;

function listMergedTags() {
  const output = execFileSync('git', ['tag', '--merged', 'HEAD', '--list', 'v*'], {
    encoding: 'utf8',
  });
  return output
    .split('\n')
    .map(value => value.trim())
    .filter(Boolean);
}

function compareVersionParts(left, right) {
  for (let index = 0; index < 3; index += 1) {
    if (left[index] !== right[index]) {
      return left[index] - right[index];
    }
  }
  return 0;
}

function selectProductTag(tags) {
  const selected = [];
  for (const tag of tags) {
    const match = PRODUCT_TAG.exec(tag);
    if (!match) {
      continue;
    }

    selected.push({
      tag,
      version: match[1],
      parts: match[1].split('.').map(Number),
    });
  }

  selected.sort((left, right) => compareVersionParts(left.parts, right.parts));
  return selected.at(-1) || null;
}

function planMobileVersion({ tags, runNumber }) {
  const selected = selectProductTag(tags);
  if (!selected) {
    throw new Error(
      'No product release tag (vX.Y.Z) is reachable from this branch. Ship a normal ci.yml release first.',
    );
  }

  const versionCode = String(runNumber || '').trim();
  if (!/^[1-9][0-9]*$/.test(versionCode)) {
    throw new Error(`GITHUB_RUN_NUMBER is not a positive integer: ${versionCode}`);
  }

  return {
    tag: selected.tag,
    version: selected.version,
    versionCode,
  };
}

function emit(result) {
  const outputFile = process.env.GITHUB_OUTPUT;
  if (outputFile) {
    fs.appendFileSync(
      outputFile,
      `source_tag=${result.tag}\nversion=${result.version}\nversion_code=${result.versionCode}\n`,
    );
  }

  console.log(
    `source_tag=${result.tag} version=${result.version} version_code=${result.versionCode}`,
  );
}

module.exports = {
  PRODUCT_TAG,
  listMergedTags,
  planMobileVersion,
  selectProductTag,
};

if (require.main === module) {
  try {
    emit(
      planMobileVersion({
        tags: listMergedTags(),
        runNumber: process.env.GITHUB_RUN_NUMBER,
      }),
    );
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }
}
