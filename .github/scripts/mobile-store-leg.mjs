#!/usr/bin/env node
// Mobile · Android and iOS store legs: decides whether this run builds and signs the store
// binary, and whether it is a mobile release.
//
// A store leg signs on every push and pull request, so a change that breaks the store build is
// red on the change that made it. It records a declared skip, never a pass, in two cases:
// GitHub withholds Actions secrets from Dependabot and from pull requests opened from forks,
// and a mobile release dispatch that ships the other platform only has nothing to build here.
// A dispatch that should sign still fails when the secrets are missing.
//
//   node .github/scripts/mobile-store-leg.mjs   (reads the environment, writes GITHUB_OUTPUT)

import { appendFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

export const RELEASE_CHANNELS = new Set(['beta', 'prod']);

export function decideStoreLeg({ event, actor, repository, headRepository, channel, platforms, platform }) {
  if (platform !== 'android' && platform !== 'ios')
    throw new Error(`Unknown store platform '${platform}'.`);
  const release = event === 'workflow_dispatch' && RELEASE_CHANNELS.has(channel);

  if (actor === 'dependabot[bot]')
    return { sign: false, release, reason: 'GitHub withholds signing secrets from Dependabot.' };
  if (headRepository && headRepository !== repository)
    return { sign: false, release, reason: 'GitHub withholds signing secrets from pull requests opened from forks.' };
  if (release && platforms !== 'both' && platforms !== platform)
    return { sign: false, release, reason: `This mobile release ships ${platforms} only.` };
  return { sign: true, release, reason: '' };
}

function main() {
  const decision = decideStoreLeg({
    event: process.env.EVENT_NAME ?? '',
    actor: process.env.ACTOR ?? '',
    repository: process.env.REPOSITORY ?? '',
    headRepository: process.env.HEAD_REPOSITORY ?? '',
    channel: process.env.MOBILE_CHANNEL ?? '',
    platforms: process.env.MOBILE_PLATFORMS ?? 'both',
    platform: process.env.STORE_PLATFORM ?? '',
  });
  const lines = [`sign=${decision.sign}`, `release=${decision.release}`, `reason=${decision.reason}`];
  if (process.env.GITHUB_OUTPUT)
    appendFileSync(process.env.GITHUB_OUTPUT, `${lines.join('\n')}\n`);
  if (!decision.sign && process.env.GITHUB_STEP_SUMMARY)
    appendFileSync(process.env.GITHUB_STEP_SUMMARY, `Skipped as declared: ${decision.reason}\n`);
  console.log(lines.join(' '));
}

if (process.argv[1] === fileURLToPath(import.meta.url))
  main();
