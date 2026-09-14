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

npm --prefix AgentUp.Mobile install --no-audit --no-fund --package-lock=false
