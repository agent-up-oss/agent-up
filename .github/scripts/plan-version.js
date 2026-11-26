'use strict';

const fs = require('fs');
const { listMergedTags, selectProductTag } = require('./plan-mobile-version.js');

function isPositiveInteger(value) {
  return /^[1-9][0-9]*$/.test(String(value || '').trim());
}

function planPrereleaseVersion({ tags, runNumber, runAttempt }) {
  const selected = selectProductTag(tags);
  const baseVersion = selected?.version || '0.0.0';
  const run = String(runNumber || '').trim();
  if (!isPositiveInteger(run)) {
    throw new Error(`GITHUB_RUN_NUMBER is not a positive integer: ${run}`);
  }

  const attemptRaw = String(runAttempt ?? '1').trim();
  if (!isPositiveInteger(attemptRaw)) {
    throw new Error(`GITHUB_RUN_ATTEMPT is not a positive integer: ${attemptRaw}`);
  }

  const attempt = Number(attemptRaw);
  const suffix = attempt > 1 ? `.${attempt}` : '';
  return {
    baseVersion,
    version: `${baseVersion}-${run}${suffix}`,
  };
}

function emit(version, publish, prerelease) {
  const outputFile = process.env.GITHUB_OUTPUT;
  if (outputFile) {
    fs.appendFileSync(
      outputFile,
      `version=${version}\npublish=${publish}\nprerelease=${prerelease}\n`,
    );
  }
  console.log(`version=${version} publish=${publish} prerelease=${prerelease}`);
}

async function planMainVersion(run) {
  const fallbackVersion = `0.0.0-ci.${run}`;
  const semanticReleaseModule = require('semantic-release');
  const semanticRelease = semanticReleaseModule.default || semanticReleaseModule;
  const result = await semanticRelease({
    dryRun: true,
    ci: false,
    branches: ['main'],
    plugins: [
      ['@semantic-release/commit-analyzer', { preset: 'conventionalcommits' }],
    ],
  });

  if (result?.nextRelease?.version) {
    return { version: result.nextRelease.version, publish: true };
  }

  return { version: fallbackVersion, publish: false };
}

async function main() {
  const run = process.env.GITHUB_RUN_NUMBER || '0';
  const isMain = process.env.GITHUB_REF === 'refs/heads/main';
  const isPush = process.env.GITHUB_EVENT_NAME === 'push';

  if (!isMain) {
    const planned = planPrereleaseVersion({
      tags: listMergedTags(),
      runNumber: process.env.GITHUB_RUN_NUMBER,
      runAttempt: process.env.GITHUB_RUN_ATTEMPT,
    });
    emit(planned.version, isPush, true);
    return;
  }

  const planned = await planMainVersion(run);
  emit(planned.version, planned.publish, false);
}

module.exports = {
  planPrereleaseVersion,
};

if (require.main === module) {
  main().catch(err => {
    console.error(err.message);
    process.exit(1);
  });
}
