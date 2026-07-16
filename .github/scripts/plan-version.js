'use strict';

const fs = require('fs');

async function main() {
  const sha = (process.env.GITHUB_SHA || 'unknown').substring(0, 8);
  const run = process.env.GITHUB_RUN_NUMBER || '0';
  const isMain = process.env.GITHUB_REF === 'refs/heads/main';

  if (!isMain) {
    emit(`0.0.0-ci.${run}.${sha}`, false);
    return;
  }

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
    emit(result.nextRelease.version, true);
  } else {
    emit(`0.0.0-ci.${run}.${sha}`, false);
  }
}

function emit(version, publish) {
  const outputFile = process.env.GITHUB_OUTPUT;
  if (outputFile) {
    fs.appendFileSync(outputFile, `version=${version}\npublish=${publish}\n`);
  }
  console.log(`version=${version} publish=${publish}`);
}

main().catch(err => {
  console.error(err.message);
  process.exit(1);
});
