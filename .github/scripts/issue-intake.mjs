import fs from "node:fs";

const API_VERSION = "2022-11-28";
const MODEL_ENDPOINT = "https://models.github.ai/inference/chat/completions";
const MODEL = "openai/gpt-4o-mini";
const MAX_TOKENS = 700;
const PROMPT_FILE = ".github/prompts/issue-intake.prompt.yml";
const PROMPT_STEM = "issue-intake";
const REVIEW_LABEL = "request ai review";

const labels = {
  intake: {
    name: "ai intake",
    color: "5319e7",
    description: "Issue should be assessed by AI",
  },
  review: {
    name: REVIEW_LABEL,
    color: "ededed",
    description: "Triggers the GitHub AI issue assessment",
  },
};

function requiredEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }

  return value;
}

function parseSystemPrompt(promptFile) {
  const lines = fs.readFileSync(promptFile, "utf8").split(/\r?\n/);
  const roleIndex = lines.findIndex((line) => /^\s*-\s+role:\s+system\s*$/.test(line));
  if (roleIndex === -1) {
    throw new Error(`System message not found in ${promptFile}`);
  }

  const contentIndex = lines.findIndex(
    (line, index) => index > roleIndex && /^\s*content:\s*\|\s*$/.test(line),
  );
  if (contentIndex === -1) {
    throw new Error(`System message content block not found in ${promptFile}`);
  }

  const contentLines = [];
  for (const line of lines.slice(contentIndex + 1)) {
    if (/^\s*-\s+role:\s+\w+/.test(line)) {
      break;
    }

    contentLines.push(line.replace(/^ {6}/, ""));
  }

  return contentLines.join("\n").trimEnd();
}

function buildIssueInput() {
  return [
    "Issue title:",
    requiredEnv("ISSUE_TITLE"),
    "",
    "Labels applied when submitted:",
    process.env.ISSUE_LABELS || "",
    "",
    "Issue description:",
    process.env.ISSUE_BODY || "(No description provided)",
  ].join("\n");
}

function extractAssessmentLabel(aiResponse) {
  const assessmentRegex = /^###.*[aA]ssessment:\s*(.+)$/;
  let assessment = "unsure";

  for (const line of aiResponse.split("\n")) {
    const match = line.match(assessmentRegex);
    if (match?.[1]?.trim()) {
      assessment = match[1].trim().toLowerCase();
    }
  }

  return `ai:${PROMPT_STEM}:${assessment}`.slice(0, 50);
}

async function githubRequest(method, path, body = undefined) {
  const token = requiredEnv("GITHUB_TOKEN");
  const response = await fetch(`https://api.github.com${path}`, {
    method,
    headers: {
      Accept: "application/vnd.github+json",
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
      "X-GitHub-Api-Version": API_VERSION,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (response.status === 204) {
    return undefined;
  }

  const text = await response.text();
  const data = text ? JSON.parse(text) : undefined;
  if (!response.ok) {
    throw new Error(`${method} ${path} failed with ${response.status}: ${text}`);
  }

  return data;
}

async function ensureLabel(owner, repo, label) {
  const path = `/repos/${owner}/${repo}/labels/${encodeURIComponent(label.name)}`;
  const existing = await fetch(`https://api.github.com${path}`, {
    headers: {
      Accept: "application/vnd.github+json",
      Authorization: `Bearer ${requiredEnv("GITHUB_TOKEN")}`,
      "X-GitHub-Api-Version": API_VERSION,
    },
  });

  if (existing.status === 200) {
    return;
  }

  if (existing.status !== 404) {
    const text = await existing.text();
    throw new Error(`GET ${path} failed with ${existing.status}: ${text}`);
  }

  await githubRequest("POST", `/repos/${owner}/${repo}/labels`, label);
}

async function callModel(systemPrompt, issueInput) {
  const response = await fetch(MODEL_ENDPOINT, {
    method: "POST",
    headers: {
      Accept: "application/vnd.github+json",
      Authorization: `Bearer ${requiredEnv("GITHUB_TOKEN")}`,
      "Content-Type": "application/json",
      "X-GitHub-Api-Version": API_VERSION,
    },
    body: JSON.stringify({
      model: MODEL,
      max_tokens: MAX_TOKENS,
      messages: [
        { role: "system", content: systemPrompt },
        { role: "user", content: issueInput },
      ],
    }),
  });

  const text = await response.text();
  if (!response.ok) {
    throw new Error(`GitHub Models request failed with ${response.status}: ${text}`);
  }

  const data = JSON.parse(text);
  const content = data.choices?.[0]?.message?.content;
  if (!content) {
    throw new Error(`GitHub Models response did not contain message content: ${text}`);
  }

  return content;
}

async function main() {
  const systemPrompt = parseSystemPrompt(PROMPT_FILE);
  const issueInput = buildIssueInput();

  if (process.env.ISSUE_INTAKE_DRY_RUN === "true") {
    const aiResponse =
      process.env.ISSUE_INTAKE_RESPONSE ||
      "### AI Assessment: Needs Clarification\n\nThe issue needs the following information:\n\n- [ ] What should happen?";
    console.log(extractAssessmentLabel(aiResponse));
    console.log(systemPrompt.split("\n")[0]);
    console.log(issueInput);
    return;
  }

  const [owner, repo] = requiredEnv("GITHUB_REPOSITORY").split("/");
  const issueNumber = Number.parseInt(requiredEnv("ISSUE_NUMBER"), 10);
  if (!owner || !repo || !Number.isInteger(issueNumber)) {
    throw new Error("GITHUB_REPOSITORY or ISSUE_NUMBER is invalid");
  }

  await ensureLabel(owner, repo, labels.intake);
  await ensureLabel(owner, repo, labels.review);
  await githubRequest("POST", `/repos/${owner}/${repo}/issues/${issueNumber}/labels`, {
    labels: [labels.intake.name, labels.review.name],
  });

  const aiResponse = await callModel(systemPrompt, issueInput);
  const assessmentLabel = extractAssessmentLabel(aiResponse);
  await ensureLabel(owner, repo, {
    name: assessmentLabel,
    color: "ededed",
    description: "AI issue intake assessment result",
  });
  await githubRequest("POST", `/repos/${owner}/${repo}/issues/${issueNumber}/labels`, {
    labels: [assessmentLabel],
  });

  if (!/<!-- no-comment -->/i.test(aiResponse)) {
    await githubRequest("POST", `/repos/${owner}/${repo}/issues/${issueNumber}/comments`, {
      body: aiResponse,
    });
  }

  await githubRequest(
    "DELETE",
    `/repos/${owner}/${repo}/issues/${issueNumber}/labels/${encodeURIComponent(REVIEW_LABEL)}`,
  );
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
