#!/usr/bin/env bash
# What the mobile app under test is built from.
#
# Two keys, because two things are worth reusing for different lengths of time.
#
# `dependencies` covers the dependency set and the app's native configuration: what the compiled
# pods and their object files are a function of. It changes only when a dependency does, so a
# change to the client relinks rather than recompiling the world.
#
# `key` covers those plus every source file that ends up bundled into the app. An exact hit means
# the app the previous run compiled is the app this run would compile, so the build is skipped
# outright - which is most pushes here, because they touch the Server, the test agents, or the
# suites rather than the client.
#
# Content, not timestamps: git already knows the blob hash of every tracked file, so this is both
# exact and nearly free.
set -euo pipefail

sum() {
  if command -v shasum >/dev/null 2>&1; then shasum -a 256; else sha256sum; fi
}

hash_of() {
  git ls-files -s -- "$@" | sum | cut -c1-16
}

dependencies="$(hash_of AgentUp.Mobile/package-lock.json AgentUp.Mobile/package.json AgentUp.Mobile/app.json)"
contents="$(hash_of AgentUp.Mobile AgentUp.AgentAuth AgentUp.DesignSystem AgentUp.WebAudit)"

{
  echo "dependencies=$dependencies"
  echo "key=$contents"
} >> "${GITHUB_OUTPUT:-/dev/stdout}"

echo "Dependency set: $dependencies"
echo "App contents:   $contents"
