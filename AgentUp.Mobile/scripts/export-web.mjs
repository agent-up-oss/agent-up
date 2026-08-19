import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { join } from 'node:path';

const git = (...args) => {
  try {
    return execFileSync('git', args, { encoding: 'utf8' }).trim();
  } catch {
    return '';
  }
};

const branch = process.env.CF_PAGES_BRANCH
  || process.env.GITHUB_REF_NAME
  || git('branch', '--show-current');
const fullSha = process.env.CF_PAGES_COMMIT_SHA
  || process.env.GITHUB_SHA
  || git('rev-parse', 'HEAD')
  || 'source';
const sha = fullSha === 'source' ? fullSha : fullSha.slice(0, 7);
const channelMatch = /^(\d+)-/.exec(branch);
const channel = process.env.EXPO_PUBLIC_AGENT_UP_CHANNEL
  || (branch === 'main' ? 'main' : channelMatch?.[1])
  || 'development';
const publishedAt = process.env.EXPO_PUBLIC_AGENT_UP_PUBLISHED_AT
  || (fullSha !== 'source' ? git('show', '-s', '--format=%cI', fullSha) : '')
  || '';

const executable = process.platform === 'win32' ? 'expo.cmd' : 'expo';
const expo = join('node_modules', '.bin', executable);
if (!existsSync(expo)) {
  throw new Error('Expo is not installed. Run npm ci before exporting the web build.');
}

console.log(`Exporting AgentUp.Mobile rc-${channel}-${sha}${branch ? ` from ${branch}` : ''}.`);
const result = spawnSync(expo, ['export', '--platform', 'web'], {
  stdio: 'inherit',
  env: {
    ...process.env,
    EXPO_PUBLIC_AGENT_UP_CHANNEL: channel,
    EXPO_PUBLIC_AGENT_UP_SHA: sha,
    EXPO_PUBLIC_AGENT_UP_PUBLISHED_AT: publishedAt,
  },
});

if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
