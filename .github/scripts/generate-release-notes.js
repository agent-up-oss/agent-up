'use strict';

const https = require('https');

const CATEGORY_ORDER = ['release:breaking', 'release:feature', 'release:improvement', 'release:fix'];

const CATEGORY_HEADINGS = {
  'release:breaking': 'Breaking changes',
  'release:feature': 'New features',
  'release:improvement': 'Improvements',
  'release:fix': 'Bug fixes',
};

function parseRepo(repositoryUrl, env) {
  if (env.GITHUB_REPOSITORY) {
    const [owner, repo] = env.GITHUB_REPOSITORY.split('/');
    return { owner, repo };
  }
  const match = repositoryUrl.match(/github\.com[/:]([\w.-]+)\/([\w.-]+?)(?:\.git)?$/);
  if (!match) throw new Error(`Cannot parse repository URL: ${repositoryUrl}`);
  return { owner: match[1], repo: match[2] };
}

function githubGet(path, token) {
  return new Promise((resolve, reject) => {
    const req = https.request(
      {
        hostname: 'api.github.com',
        path,
        method: 'GET',
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: 'application/vnd.github+json',
          'X-GitHub-Api-Version': '2022-11-28',
          'User-Agent': 'agent-up-release-notes',
        },
      },
      res => {
        const chunks = [];
        res.on('data', chunk => chunks.push(chunk));
        res.on('end', () => {
          const body = Buffer.concat(chunks).toString();
          if (res.statusCode >= 200 && res.statusCode < 300) {
            resolve(JSON.parse(body));
          } else {
            reject(new Error(`GitHub API ${path} → ${res.statusCode}: ${body}`));
          }
        });
      },
    );
    req.setTimeout(15_000, () => {
      req.destroy(new Error(`GitHub API request timed out: ${path}`));
    });
    req.on('error', reject);
    req.end();
  });
}

async function getPrsForCommit(owner, repo, sha, token) {
  const prs = await githubGet(
    `/repos/${owner}/${repo}/commits/${sha}/pulls?per_page=100`,
    token,
  );
  return Array.isArray(prs) ? prs.filter(pr => pr.merged_at) : [];
}

async function generateNotes(pluginConfig, context) {
  const { commits, options, env, logger } = context;
  const token = env.GITHUB_TOKEN;

  if (!token) {
    throw new Error('GITHUB_TOKEN is required for release notes generation');
  }

  const { owner, repo } = parseRepo(options.repositoryUrl, env);

  logger.log(`Resolving pull requests for ${commits.length} commits`);

  const prMap = new Map();

  await Promise.all(
    commits.map(async commit => {
      const prs = await getPrsForCommit(owner, repo, commit.hash, token);
      for (const pr of prs) {
        if (!prMap.has(pr.number)) {
          prMap.set(pr.number, pr);
        }
      }
    }),
  );

  logger.log(`Resolved ${prMap.size} unique pull requests`);

  const categorized = new Map();

  for (const pr of prMap.values()) {
    const labels = pr.labels.map(l => l.name);
    if (!labels.includes('release-note')) continue;

    const category = CATEGORY_ORDER.find(cat => labels.includes(cat));
    if (!category) continue;

    if (!categorized.has(category)) categorized.set(category, []);
    categorized.get(category).push(pr);
  }

  for (const prs of categorized.values()) {
    prs.sort((a, b) => a.number - b.number);
  }

  const sections = [];

  for (const category of CATEGORY_ORDER) {
    const prs = categorized.get(category);
    if (!prs || prs.length === 0) continue;

    const lines = [`## ${CATEGORY_HEADINGS[category]}`, ''];
    for (const pr of prs) {
      lines.push(
        `- ${pr.title} ([#${pr.number}](https://github.com/${owner}/${repo}/pull/${pr.number}))`,
      );
    }
    sections.push(lines.join('\n'));
  }

  if (sections.length === 0) {
    return 'This release contains internal maintenance and reliability improvements.';
  }

  return sections.join('\n\n');
}

module.exports = { generateNotes };
