#!/usr/bin/env bash
# Source · TypeScript and design-system suites: every TypeScript client module, the design
# system's own suite, the end-to-end harness, and this workflow's own scripts. The CI runner has
# no Nix, so the commands the repository npm scripts wrap in nix-shell run directly.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

echo "::group::Install mobile dependencies"
./.github/scripts/install-mobile-deps.sh
echo "::endgroup::"

echo "::group::Mobile, Chat, ServerClient and AgentAuth"
npm --prefix AgentUp.AgentAuth test
(
  cd AgentUp.Mobile
  npx tsc --noEmit -p tsconfig.test.json
  npx tsc --noEmit -p ../AgentUp.ServerClient/tsconfig.json
  npx tsx --test ../AgentUp.ServerClient/src/*.test.ts
  npx tsx --test ../AgentUp.Chat/src/providers/*.test.ts
  npx tsx --test src/features/**/*.test.ts
  node --test scripts/*.test.mjs
)
echo "::endgroup::"

echo "::group::WebAudit"
npm --prefix AgentUp.WebAudit ci
npm --prefix AgentUp.WebAudit test
echo "::endgroup::"

# One palette, catalog-only paint, the scenes the command centers mount: the rules that keep the
# design system the only visual source. This suite never ran in CI before, and had drifted.
echo "::group::Design system"
npm --prefix AgentUp.DesignSystem ci
npm --prefix AgentUp.DesignSystem test
echo "::endgroup::"

echo "::group::Chat module and the app that harnesses it"
npm --prefix AgentUp.Chat run typecheck
npm --prefix AgentUp.Mobile.E2E.App install --no-audit --no-fund
npx --prefix AgentUp.Mobile.E2E.App tsc --noEmit -p AgentUp.Mobile.E2E.App/tsconfig.json
echo "::endgroup::"

# Cloudflare Pages builds the web client with one script and cannot build sibling packages, so
# their dist/ is committed. That only works while it is in step with src.
echo "::group::Committed AgentAuth build is current"
npm --prefix AgentUp.AgentAuth run build
git diff --exit-code -- AgentUp.AgentAuth/dist
echo "::endgroup::"

echo "::group::End-to-end harness"
(cd AgentUp.Mobile.E2E && node scripts/forbid-sleep.mjs && node --test test/*.test.mjs)
echo "::endgroup::"

# The sign-in legs run these scripts on a macOS runner, whose bash is 3.2.
echo "::group::This workflow's own scripts and behavior matrix"
node .github/scripts/check-shell-portability.mjs
node --test .github/scripts/*.test.mjs .github/scripts/*.test.js
./.github/scripts/install-ubuntu-browser-test-deps.test.sh
node .github/scripts/behavior-matrix.mjs check
node .github/scripts/backend-parity.mjs check
echo "::endgroup::"
