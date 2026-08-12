'use strict';

async function main() {
  const semanticReleaseModule = require('semantic-release');
  const semanticRelease = semanticReleaseModule.default || semanticReleaseModule;

  await semanticRelease({
    tagFormat: 'localinstaller-v${version}',
    branches: ['main'],
    plugins: [
      ['@semantic-release/commit-analyzer', { preset: 'conventionalcommits' }],
      [
        '@semantic-release/github',
        {
          assets: [
            { path: 'nupkgs/*.nupkg', label: 'NuGet packages' },
            { path: 'release-artifacts/*', label: 'LocalInstaller sample installers' },
          ],
        },
      ],
      [
        '@semantic-release/exec',
        {
          publishCmd:
            '[ -z "$NUGET_API_KEY" ] || dotnet nuget push nupkgs/*.nupkg' +
            ' --api-key "$NUGET_API_KEY"' +
            ' --source https://api.nuget.org/v3/index.json' +
            ' --skip-duplicate',
        },
      ],
    ],
  });
}

main().catch(err => {
  console.error(err.message);
  process.exit(1);
});
