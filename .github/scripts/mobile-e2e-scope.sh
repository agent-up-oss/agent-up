#!/usr/bin/env bash
# Decides whether the mobile end-to-end suites need to run for this push.
#
# They hold three runners for tens of minutes, one of them macOS, so running them for a push that
# cannot affect them is pure waste. Anything that can change what they exercise counts: the client,
# the shared sign-in module, the Server, the test agents, the suites themselves, or the workflow.
set -euo pipefail

paths=(
  "AgentUp.Mobile/"
  "AgentUp.Mobile.E2E/"
  "AgentUp.AgentAuth/"
  "AgentUp.TestAgents/"
  "AgentUp.Server/"
  "AgentUp.DesignSystem/"
  ".github/workflows/ci.yml"
  ".github/scripts/build-mobile-e2e-stack.sh"
  ".github/scripts/install-mobile-deps.sh"
  ".github/scripts/mobile-e2e-scope.sh"
)

before="${GITHUB_EVENT_BEFORE:-}"
if [ -z "$before" ] || [ "$before" = "0000000000000000000000000000000000000000" ] \
  || ! git cat-file -e "$before^{commit}" 2>/dev/null; then
  # A new branch, a force push, or a shallow history: there is nothing to diff against, so run
  # them rather than silently skipping a suite that might have been affected.
  echo "No comparable previous commit; running the mobile end-to-end suites."
  echo "affected=true" >> "${GITHUB_OUTPUT:-/dev/stdout}"
  exit 0
fi

changed="$(git diff --name-only "$before" "${GITHUB_SHA:-HEAD}")"

for path in "${paths[@]}"; do
  if printf '%s\n' "$changed" | grep -q "^${path}"; then
    echo "Changed under ${path}; running the mobile end-to-end suites."
    echo "affected=true" >> "${GITHUB_OUTPUT:-/dev/stdout}"
    exit 0
  fi
done

echo "Nothing the mobile end-to-end suites cover has changed; skipping them."
echo "affected=false" >> "${GITHUB_OUTPUT:-/dev/stdout}"
