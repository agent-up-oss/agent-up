#!/usr/bin/env bash
set -euo pipefail

# Proves the agent sandbox with real bubblewrap and real Linux mounts: a dev reviewer cannot
# write its clone, an author cannot write its Git history, and no agent sees the Server's data,
# another workspace, or the Server's secrets. bubblewrap comes from Nix, as it does for the Server.

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"
results="artifacts/test-results/agent-sandbox"
mkdir -p "$results"

# Ubuntu 24.04 runners refuse unprivileged user namespaces to binaries without an AppArmor
# profile, and Nix's bwrap has none. Lift that restriction for the run when it is present.
if [ -e /proc/sys/kernel/apparmor_restrict_unprivileged_userns ] && command -v sudo >/dev/null; then
  sudo sysctl -w kernel.apparmor_restrict_unprivileged_userns=0
fi

nix-shell -p bubblewrap --run "dotnet test AgentUp.Server.Tests/AgentUp.Server.Tests.csproj \
  --results-directory $results \
  --logger 'trx;LogFileName=agent-sandbox.trx' \
  --filter 'Category=AgentSandboxE2E'"
