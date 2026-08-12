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
            { path: 'nupkgs/LocalInstaller.Core.*.nupkg', label: 'LocalInstaller.Core NuGet package' },
            { path: 'nupkgs/LocalInstaller.App.*.nupkg', label: 'LocalInstaller.App NuGet package' },
            { path: 'nupkgs/LocalInstaller.Packaging.*.nupkg', label: 'LocalInstaller.Packaging NuGet package' },
            { path: 'nupkgs/LocalInstaller.Smoke.*.nupkg', label: 'LocalInstaller.Smoke NuGet package' },
            { path: 'release-artifacts/localinstaller-sample-ubuntu-linux-x64.deb', label: 'LocalInstaller sample Ubuntu (x64) .deb' },
            { path: 'release-artifacts/localinstaller-sample-macos-osx-arm64.pkg', label: 'LocalInstaller sample macOS Apple Silicon .pkg' },
            { path: 'release-artifacts/localinstaller-sample-macos-osx-x64.pkg', label: 'LocalInstaller sample macOS Intel .pkg' },
            { path: 'release-artifacts/localinstaller-sample-windows-win-x64.exe', label: 'LocalInstaller sample Windows (x64) installer' },
            { path: 'release-artifacts/localinstaller-sample-windows-win-x64.msi', label: 'LocalInstaller sample Windows (x64) MSI sidecar' },
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
