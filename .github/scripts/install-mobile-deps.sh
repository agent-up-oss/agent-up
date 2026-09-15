#!/usr/bin/env bash
# Installs the mobile client's dependencies for CI.
#
# The repository's npm scripts route through nix-shell, which the mobile CI runners do not have
# and which would fight the Xcode and Android toolchains they do have. The commands underneath are
# the same ones those scripts run; only the shell wrapper is skipped.
set -euo pipefail

node AgentUp.DesignSystem/scripts/build.mjs

npm --prefix AgentUp.WebAudit install --no-audit --no-fund
npm --prefix AgentUp.WebAudit run build

npm --prefix AgentUp.AgentAuth install --no-audit --no-fund
npm --prefix AgentUp.AgentAuth run build

# The lockfile governs, deliberately. Installing without it resolves the ranges afresh - which
# moved 77 packages the last time the two were compared - so the app CI built was not the app the
# lockfile describes, while the cache key hashes that lockfile. A key that cannot predict the
# bytes it stands for is worse than no cache at all.
npm --prefix AgentUp.Mobile install --no-audit --no-fund
