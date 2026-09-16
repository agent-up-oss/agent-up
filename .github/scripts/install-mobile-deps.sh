#!/usr/bin/env bash
# Installs the dependencies of the app under test for CI.
#
# Which app that is depends on the caller: the sign-in suites build the chat harness, and the
# mobile job builds the full client. Pass the directory; both pull the same shared modules first.
#
# The repository's npm scripts route through nix-shell, which the mobile CI runners do not have
# and which would fight the Xcode and Android toolchains they do have. The commands underneath are
# the same ones those scripts run; only the shell wrapper is skipped.
set -euo pipefail

app="${1:-AgentUp.Mobile}"

node AgentUp.DesignSystem/scripts/build.mjs

npm --prefix AgentUp.WebAudit install --no-audit --no-fund
npm --prefix AgentUp.WebAudit run build

npm --prefix AgentUp.AgentAuth install --no-audit --no-fund
npm --prefix AgentUp.AgentAuth run build

# Source-only modules. They need their own file: dependencies on disk or nothing can resolve
# @agent-up/* from inside them - the bundler reads a module's imports from where the module lives,
# not from where the app does. Their .npmrc keeps the peers out, so no second react comes with it.
npm --prefix AgentUp.ServerClient install --no-audit --no-fund
npm --prefix AgentUp.Chat install --no-audit --no-fund

# The lockfile governs, deliberately. Installing without it resolves the ranges afresh - which
# moved 77 packages the last time the two were compared - so the app CI built was not the app the
# lockfile describes, while the cache key hashes that lockfile. A key that cannot predict the
# bytes it stands for is worse than no cache at all.
npm --prefix "$app" install --no-audit --no-fund
