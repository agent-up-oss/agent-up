#!/usr/bin/env bash
# Mobile · Browser SSO front door: the installable web client signs in through
# Examples/browser-sso, which advertises browserSso and returns a restricted entitlement
# document, and must hide what that document forbids. Runs the example's own HTTP tests first.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

echo "::group::Export the Mobile web client"
./.github/scripts/install-mobile-deps.sh AgentUp.Mobile
(cd AgentUp.Mobile && node scripts/export-web.mjs)
echo "::endgroup::"

cd Examples/browser-sso
echo "::group::Install the example and its browser"
npm ci
npx playwright install --with-deps chromium
echo "::endgroup::"

npm test
AGENTUP_MOBILE_WEB_EXPORT="$root/AgentUp.Mobile/dist" npx playwright test --config e2e/playwright.config.mjs
