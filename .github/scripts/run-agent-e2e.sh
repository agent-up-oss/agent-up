#!/usr/bin/env bash
set -euo pipefail

# Agents against the real Server, with nothing faked:
#   AgentSandboxE2E - real bubblewrap and real mounts. A dev reviewer cannot write its clone, an
#                     author cannot write its Git history, and no agent sees the Server's data,
#                     another workspace, or the Server's secrets.
#   AgentMcpE2E     - the sandboxed test-agent-scripted drives a real Server through the MCP
#                     servers it is offered: it starts a real app, then navigates, inspects and
#                     clicks it in the real headless browser. Its MCP session is pinned to its
#                     own workspace, and a dev reviewer is refused orchestration, the browser and
#                     the commit queue.
#   TeamForgejoE2E  - an author, a dev reviewer and a PM reviewer on one Server take an issue
#                     through a real Forgejo container to two approvals: assignment, task branch,
#                     proposal, requested reviewers, a finding at its line, the fix going back to
#                     the same reviewers, and a screenshot attached to the product review.
# bubblewrap and Chromium come from Nix, as they do for the Server.
#
# With no argument all three run, which is what the agent-e2e verification check does. CI runs
# one category per Integration leg so a red leg names the dependency that broke:
#   ./.github/scripts/run-agent-e2e.sh AgentSandboxE2E

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

categories=("$@")
if [ "${#categories[@]}" -eq 0 ]; then
  categories=(AgentSandboxE2E AgentMcpE2E TeamForgejoE2E)
fi
filter=""
for category in "${categories[@]}"; do
  case "$category" in
    AgentSandboxE2E|AgentMcpE2E|TeamForgejoE2E) ;;
    *) echo "Unknown agent E2E category: $category" >&2; exit 2 ;;
  esac
  filter="${filter:+$filter|}Category=$category"
done
trx="agent-e2e.trx"
if [ "${#categories[@]}" -eq 1 ]; then
  trx="${categories[0]}.trx"
fi

results="artifacts/test-results/agent-e2e"
agents="$root/artifacts/agent-e2e/test-agents"
mkdir -p "$results" "$agents"

# Ubuntu 24.04 runners refuse unprivileged user namespaces to binaries without an AppArmor
# profile, and Nix's bwrap has none. Lift that restriction for the run when it is present.
if [ -e /proc/sys/kernel/apparmor_restrict_unprivileged_userns ] && command -v sudo >/dev/null; then
  sudo sysctl -w kernel.apparmor_restrict_unprivileged_userns=0
fi

dotnet publish AgentUp.TestAgents/AgentUp.TestAgents.csproj --configuration Release --output "$agents"
export AGENTUP_AGENT_E2E_TEST_AGENTS="$agents/AgentUp.TestAgents"

nix-shell -p bubblewrap chromium --run "export AGENTUP_AGENT_E2E_CHROMIUM=\"\$(command -v chromium)\"; \
  dotnet test AgentUp.Server.Tests/AgentUp.Server.Tests.csproj \
  --results-directory $results \
  --logger 'trx;LogFileName=$trx' \
  --blame-hang-timeout 15m \
  --filter '$filter'"
